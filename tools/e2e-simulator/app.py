from __future__ import annotations

import asyncio
import importlib
import queue
import threading
import time

import streamlit as st

import e2e_simulator.config as config_module
importlib.reload(config_module)
from e2e_simulator.config import load_config
from e2e_simulator.models import SimulationModule
from e2e_simulator.orchestrator import SimulationOrchestrator, save_report

st.set_page_config(
    page_title="Financial-Core E2E Simulator",
    page_icon="🧪",
    layout="wide",
)

st.title("Financial-Core E2E Workflow Simulator")
st.caption("API-driven bulk seeding for Investments, Guarantees, and Loans")

if "logs" not in st.session_state:
    st.session_state.logs = []
if "report" not in st.session_state:
    st.session_state.report = None
if "running" not in st.session_state:
    st.session_state.running = False
if "stop_flag" not in st.session_state:
    st.session_state.stop_flag = False
if "batch_error" not in st.session_state:
    st.session_state.batch_error = None
if "batch_thread" not in st.session_state:
    st.session_state.batch_thread = None
if "log_queue" not in st.session_state:
    st.session_state.log_queue = queue.Queue()
if "progress_pct" not in st.session_state:
    st.session_state.progress_pct = 0.0
if "progress_message" not in st.session_state:
    st.session_state.progress_message = "Idle"

config = load_config()
_default_seed_admin = getattr(config, "seed_admin_phone", "") or "09358357344"

with st.sidebar:
    st.header("Configuration")
    base_url = st.text_input("API base URL", value=config.base_url)
    dev_otp = st.text_input("Dev OTP", value=config.dev_otp, type="password")
    seed_admin_phone = st.text_input(
        "Seed admin phone",
        value=_default_seed_admin,
        help="Must match Otp:SeedAdminPhone in Core API appsettings.json",
    )
    cases_per_module = st.number_input(
        "Cases per module",
        min_value=1,
        max_value=500,
        value=config.default_cases_per_module,
    )
    max_concurrent = st.slider(
        "Max concurrent cases",
        min_value=1,
        max_value=20,
        value=config.max_concurrent_cases,
    )
    st.subheader("Path toggles")
    path_happy = st.checkbox("Happy path (40%)", value=True)
    path_revision = st.checkbox("Revision loop (40%)", value=True)
    path_rejection = st.checkbox("Rejection (10%)", value=True)
    path_enrichment = st.checkbox("Enrichment (10%)", value=True)

    run_investment = st.checkbox("Seed Investments", value=True)
    run_guarantee = st.checkbox("Seed Guarantees", value=True)
    run_loan = st.checkbox("Seed Loans", value=True)

    st.info(
        "Provisioning sends real OTP SMS via Kavenegar per login. "
        "Step 2/4 (bootstrap) is the first pause — usually 10–30 seconds."
    )

col_run, col_stop = st.columns([1, 1])
progress_bar = st.progress(0.0, text="Idle")
status_box = st.empty()
log_area = st.container()

with col_run:
    start_clicked = st.button("Run selected modules", type="primary", disabled=st.session_state.running)
with col_stop:
    stop_clicked = st.button("Stop after current cases", disabled=not st.session_state.running)


def append_log(message: str) -> None:
    st.session_state.logs.append(message)
    if len(st.session_state.logs) > 500:
        st.session_state.logs = st.session_state.logs[-500:]


def drain_log_queue() -> int:
    q: queue.Queue = st.session_state.log_queue
    drained = 0
    while True:
        try:
            item = q.get_nowait()
        except queue.Empty:
            break
        drained += 1
        if item[0] == "log":
            append_log(item[1])
        elif item[0] == "progress":
            st.session_state.progress_pct = item[1]
            st.session_state.progress_message = item[2]
    return drained


def ui_log(message: str) -> None:
    st.session_state.log_queue.put(("log", message))


def ui_progress(pct: float, message: str) -> None:
    st.session_state.log_queue.put(("progress", pct, message))


async def execute_batch() -> None:
    runtime_config = load_config()
    runtime_config.base_url = base_url.rstrip("/")
    runtime_config.dev_otp = dev_otp
    runtime_config.seed_admin_phone = seed_admin_phone.strip()
    runtime_config.default_cases_per_module = int(cases_per_module)
    runtime_config.max_concurrent_cases = int(max_concurrent)

    modules: list[SimulationModule] = []
    if run_investment:
        modules.append(SimulationModule.INVESTMENT)
    if run_guarantee:
        modules.append(SimulationModule.GUARANTEE)
    if run_loan:
        modules.append(SimulationModule.LOAN)

    if not modules:
        ui_log("No modules selected.")
        return

    stop_event = asyncio.Event()
    if st.session_state.stop_flag:
        stop_event.set()

    orchestrator = SimulationOrchestrator(
        runtime_config,
        log=ui_log,
        progress=ui_progress,
        stop_event=stop_event,
    )
    enabled_paths = {
        "happy": path_happy,
        "revision": path_revision,
        "rejection": path_rejection,
        "enrichment": path_enrichment,
    }
    report = await orchestrator.run_modules(
        modules,
        int(cases_per_module),
        enabled_paths=enabled_paths,
    )
    json_path, csv_path = save_report(report, runtime_config.reports_path)
    st.session_state.report = report
    ui_log(f"Saved JSON report: {json_path}")
    ui_log(f"Saved CSV report: {csv_path}")


def _run_batch_in_thread() -> None:
    try:
        asyncio.run(execute_batch())
    except Exception as exc:
        st.session_state.batch_error = str(exc)
    finally:
        st.session_state.running = False


if stop_clicked:
    st.session_state.stop_flag = True
    append_log("Stop requested.")

if start_clicked and not st.session_state.running:
    st.session_state.running = True
    st.session_state.stop_flag = False
    st.session_state.batch_error = None
    st.session_state.logs = []
    st.session_state.report = None
    st.session_state.progress_pct = 0.0
    st.session_state.progress_message = "Starting…"
    append_log("Starting batch run…")
    thread = threading.Thread(target=_run_batch_in_thread, daemon=True)
    st.session_state.batch_thread = thread
    thread.start()

if st.session_state.running:
    drain_log_queue()
    pct = float(st.session_state.progress_pct)
    msg = st.session_state.progress_message
    progress_bar.progress(min(max(pct, 0.0), 1.0), text=msg)
    status_box.info(msg)
    time.sleep(0.8)
    st.rerun()
else:
    drain_log_queue()
    pct = float(st.session_state.progress_pct)
    if pct > 0:
        progress_bar.progress(1.0, text="Finished")
    if st.session_state.batch_error:
        status_box.error(st.session_state.batch_error)
        append_log(f"Fatal error: {st.session_state.batch_error}")

with log_area:
    st.subheader("Live log")
    st.code("\n".join(st.session_state.logs[-200:]) or "No activity yet.", language="text")

report = st.session_state.report
if report:
    st.subheader("Execution summary")
    m1, m2, m3, m4 = st.columns(4)
    m1.metric("Attempted", report.cases_attempted)
    m2.metric("Succeeded", report.cases_succeeded)
    m3.metric("Failed", report.cases_failed)
    m4.metric(
        "Success rate",
        f"{(report.cases_succeeded / report.cases_attempted * 100):.1f}%"
        if report.cases_attempted
        else "—",
    )

    c1, c2 = st.columns(2)
    with c1:
        st.markdown("**Cases by path**")
        st.json(report.path_breakdown)
        st.markdown("**Average steps by path**")
        st.json(report.avg_steps_by_path)
    with c2:
        st.markdown("**Cases by final status**")
        st.json(report.status_breakdown)

    if report.api_errors:
        st.markdown("**API errors (sample)**")
        st.dataframe([e.__dict__ for e in report.api_errors[:50]], use_container_width=True)
