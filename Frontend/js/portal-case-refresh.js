(function () {
  function delay(ms) {
    return new Promise(function (resolve) {
      setTimeout(resolve, ms);
    });
  }

  /**
   * Re-fetch and re-render until case status changes (handles read-after-write lag).
   */
  async function refreshUntilChanged(refreshOnce, getStatus, previousStatus, options) {
    options = options || {};
    const maxAttempts = options.maxAttempts || 4;
    const baseDelayMs = options.baseDelayMs || 120;
    let current = previousStatus;
    let changed = false;

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
      await refreshOnce();
      current = getStatus();
      changed = previousStatus == null ? true : current !== previousStatus;
      if (changed || attempt === maxAttempts - 1) {
        return { current: current, changed: changed, attempts: attempt + 1 };
      }
      await delay(baseDelayMs * (attempt + 1));
    }

    return { current: current, changed: changed, attempts: maxAttempts };
  }

  /** Coalesce concurrent refresh calls instead of dropping them. */
  function createCoalescedRefresh(runRefresh) {
    var pending = null;
    return function coalescedRefresh() {
      var args = arguments;
      if (pending) {
        return pending;
      }
      pending = Promise.resolve()
        .then(function () {
          return runRefresh.apply(null, args);
        })
        .finally(function () {
          pending = null;
        });
      return pending;
    };
  }

  window.PortalCaseRefresh = {
    delay: delay,
    refreshUntilChanged: refreshUntilChanged,
    createCoalescedRefresh: createCoalescedRefresh,
  };
})();
