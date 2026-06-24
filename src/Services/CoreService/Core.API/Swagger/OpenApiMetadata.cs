namespace Core.API.Swagger;

internal static class OpenApiMetadata
{
    public const string Title = "Financial Core API";
    public const string Version = "v1";

    public const string Description = """
        Backend API for fund operations — investment, guarantee, and loan workflows.

        ### Modules (v1)

        - **Identity** — OTP login, JWT sessions, roles (`/identity/users`)
        - **Companies** — applicant profiles (`/identity/companies`)
        - **Investment cases** — workflow, documents, reviews, CEO approval (`/investmentcases`)
        - **Guarantee cases** — applications, credit limits, workflow (`/guaranteecases`)
        - **Loan cases** — loan applications and workflow (`/loancases`)
        - **Case stage rollback** — admin/technical backward stage correction (`/casestages`)
        - **Kanban** — action-required and watching queues (`/kanban`)
        - **Dashboard** — CEO and board metrics (`/dashboard`)

        ### Usage

        - **Base path:** `/api/v1.0`
        - **Responses:** `ApiOperationResult<T>` (`success`, `message`, `data`, …)
        - **Auth:** click **Authorize** and paste the **access token only** (do not type `Bearer`). Token from `POST /api/v1.0/identity/users/verify-otp`.
        - **Uploads:** presign → PUT to object storage → confirm

        ### Developer

        Mohammadreza Javaheri — [mohammad.r.javaheri@gmail.com](mailto:mohammad.r.javaheri@gmail.com) — +98 935 835 7344
        """;
}
