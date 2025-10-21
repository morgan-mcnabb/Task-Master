# Task-Master -- Full-Stack ToDo App (.NET 9 Web API + Vue 3)

## Stack
- **Backend**: ASP.NET Core .NET 9, EF Core (SQLite by default), cookie session authentication
- **Frontend**: Vue 3 + Vite, Pinia, Tailwind v4
- **Deployment**: Containerized; Deployed to Fly.io for free-ish hosting. Link: https://task-master-4jz3va.fly.dev

## Quick start (local)
### Prerequisites:
- **.NET SDK 9**
- **NODE 20+ and npm**

1) Start the API
The API listens on http://localhost:5055 (check launchSettings.json)

```
cd Task-Master/TaskMaster/TaskMasterApi
dotnet restore
dotnet run
```
Or open it in your IDE of choice (I use Rider) and run the project!

2) Start the web appliation
This runs on http://localhost:5173
```
cd Task-Master/apps/ui
npm install
npm run dev
```

---
## Production build (served by the API Project)
```
cd Task-Master/apps/ui
npm run build
cd ../../TaskMaster/TaskMasterApi
dotnet run
```
The `npm run build` emits to ../TaskMaster/TaskMasterApi/wwwroot and then the .NET Web API serves both the SPA and the API from one process.
You can also build the container with the provided `Dockerfile`. I ran the container on **Fly.io**: https://task-master-4jz3va.fly.dev

---
## Assumptions
- **User accounts only**: For an MVP, having a user account and having tasks tied explicitly to that user account make sense. It will be easily extendable in the future to make the tasks viewable by potentially other users or supporting multi-tenancy and RBAC.
- **Auth**: Since the tasks are tied to specific user accounts, we need to know the context of the user that is currently logged in. So we need some authentication and a login flow. Instead of hooking it up with JWT/OIDC, I did cookie sessions to have some security and be able to isolate the information based on the current logged in user.
  - Basic username + password
  - Password storage uses modern hashing and any forgot password/email verification is out of scope for MVP.
  - cookie-based session so no third party IdP in the MVP.
- **Tasks**: Tasks should have a `title`, `description`, `priority`, `status`, `due date`, and `tags`. For the MVP, no subtasks, attachments, reminders or notifications yet.
  - Status should have the following: `Todo`, `InProgress`, `Done`, `Archived`.
  - Priority should have the following: `Low`, `Medium`, `High`
  - Tags are free text strings that are de-duplicated so as to not clutter the UI or the DB. No tag merging or renaming in MVP.
  - Tasks should be editable after being created
- **Search/filter**: a simple substring search on title and then some server-side filtering by status/priority/tag/due date with some very basic pagination + sorting.

## Trade-offs
- **SQLite**: SQLite let me ship a DB with migrations without running a server. This is great for small scale applications and getting an MVP out the door. However, there is limited concurrency and tooling for SQLite when compared to PostgreSQL or SQL Server. I'd switch the database the moment the application needed autoscaling or indexing.
- **Cookie-based auth vs JWT/OIDC**: Cookie-based auth was simpler end-to-end, the SPA and the API share an origin so these cookies just work without CORS. This also let me keep the authentication logic small for an MVP. However, with JWT/OIDC I would get statelessness and better federation, plus some extra tooling and niceties out of the box. This would be one of the first things I'd work on to improve the application.
- **Serving the SPA from the API**: I did this because it turned it into one process and one deploy. It lets the application avoid CORS as well, which can be a headache. This causes me to give up CDN edge caching and an independent deploy cadence for the UI vs the API. I'd immediately make this an S3+Cloudfront for the assets to take advantage of CDN edge caching.
- **Project Layering**: I split up this small todo app into 4 projects: Domain, Application, Infrastructure, and API. This is definitely over-engineering and a lot of overhead for a weekend project, meaning I likely spent more time on the architecture than I should have. However, if this codebase continued to grow then this project structure will pay off quickly. I tried to keep it light to stay productive and quick, but still it added a lot of time overhead than if I just had a single large project.

## Scalabilty
### What already scales
- **API Instances**: The API is containerized and we don'e keep per-user state in memory as authentication uses an encrypted cookie. The data-protection key right is persisted so any instance can validate cookies. That means that we can run multiples API replicas behind a load balancer without sticky sessions.
- **Static assets**: The SPA is prebuilt and served from `wwwroot/`. Filenames are content-hashed, so we can safely put a CDN in front with long cache TTL's, making it fast and reliable.
### Where it will bend first
- **SQLite**: This is good for small scale applications and demos but it doesn't have great support for sustained write throughput or if there are a lot of concurrent writers. This will be a bottleneck long before the API does.
- **Query patterns with tags**: Filtering by multiple tags involves joins on `TaskTag`. If there are not indexes there, these queries will massively slow down as the data grows.
- **High-reads**: If the UI were to poll or request things aggressively (right now, thankfully, it does not), we could see an avoidable heavy read load.

## What would I change with more time?
This isn't the exact order I'd make these changes, just what the order that I thought of them.
- **Move to a network database**: I'd switch from SQLite to PostgreSQL or SQL Server. I've already got the migrations so this would mostly be connection strings and provider tweaks. This would let me add appropriate indexes to make queries nice and fast. If I were using AWS we could use Aurora for PostgreSQL and turn on multi-AZ, automatic backups, and point in time recovery.
- **Scaling reads**: In case traffic massively picks up on this todo app, creating read replicas (after we've migrated to PostgreSQL or SQL Server) would make this trivial and keep our application responsive and the user experience nice.
- **Token bucket rate limiting**: ASP.NET Core allows
- **Proper identity and password flows**: I'd swap the hand-rolled auth for an external IdP like Okta or AWS Cognito (or ASP.NET Core Identity - though, to be honest, I don't have much experience with it). I'd get password hashing and rotation, lockout, 2FA and session invalidation out of the box.
- **Observability**: While I do already have request logging, I think wiring up something like OpenTelemetry for the metrics, traces and logs would amplify the debugging and support process significantly. It'll help identify whether it was the DB, App, or something else entirely that is causing problems.
- **CI/CD**: I'd like to add a pipeline that would run the unit/integration tests, builds Docker, fails on an Migration drift, and whatever else I can think of at the time. This will help keep things predictable for deployments.
- **CDN**: Since the UI is being served up via static assets, we could serve them up via a CDN to really speed up the delivery of the assets. If using AWS, we could use Cloudfront + S3.
- **Bulk operations**: Right now, the UI is limited to single entity changes. Being able to select multiple tasks to delete/update would massively improve user experience.
- **Move deployment**: The application is has a docker image hosted in Fly.io which is great for small applications and demos, like this one is right now. As it scales, I'd like to have more control over it and also take advantage of other services that AWS offers so I would deploy application to ECS Fargate behind an ALB (or not, if I don't have the money!).
---
## Architectural Designs
### Projects
- The **Domain** project holds business types and rules: `TaskItem`, `Tag`, `TaskTag` (many-to-many), `TaskStatus`/`TaskPriority`, `TaskQuery`, and a small `PagedResult`. No EntityFramework attributes or HTTP concerns. This project just expresses invariants and behavior without runtime frameworks so we canr eason about it and unit test it in isolation.
- The **Application** project defines use cases (`TaskService`, `ITaskServic`e) and request/response models (`CreateTaskModel`, `UpdateTaskModel`). It throws app-level exceptions (`NotFoundException`, `ConcurrencyException`) and depends only on domain abstractions. This lets the API layer stay thin as the orchestration, validation and policies all live here.
- The **Infrastructure** project provides EntityFramework Core (`ApplicationDbContext`), repositories (`EfTaskRepository`, `EfTagRepositor`y), migrations, and a `UnitOfWork`. All DB specifics are kept behind interfaces so the Application layer stays portable.
- The **API** project hosts controllers, mapping, middleware, and FluentValidation. It doesn’t do any business logic, instead it translates HTTP to the Application and enforces HTTP semantics so everything has clear responsibilities.

### Persistence model
- **EntityFramework Core + SQLite** with migrations in `Infrastructure/Migrations`. The repositories expose domain-friendly methods and accept a `TaskQuery` for sorting, paging, etc. SQLite keeps this project lite and the repositories prevent EntityFramework specific constructs from leaking to other projects.
- **Unit of Work** encapsulates any SaveChanges and transactions to give the Application layer a unit for multi-repository operations without having to couple to the DbContext. Kinda works? Was probably overengineering for a production MVP, but I wanted to do it so I did.

### Authentication
- **Cookie session** with `POST /auth/login`, `GET /auth/me`, `POST /auth/logout`, `POST /auth/register`. I chose this for a Production MVP so I can still force some security and user-based access without having to do any token plumbing. Given more time, I would front this with OIDC/JWT, so still cookie based but has way more fine grained control and security.

### Observability
- **Correlation ID** middleware stamps every request and response with an `X-Correlation-Id`. This makes it pretty easy to look up user issues and stitch together any logs, making support much more efficient. The frontend also generates a per-tab correlation Id so parallel tabs are distinguishable.

### Frontend design, state & routing strategy
- **Design**: I chose Tailwindcss for it's simplicity in creating a dynamic design. Since this project was time boxed, I didn't want to manually have to create my own styles as that can be extremely time consuming. Since time was of the essence, it was a no-brainer to incorporate Tailwindcss for the design framework.
- **URL = state** on the list view. All controls write to the router query and a single watcher reacts to route changes and fetches. I did this mainly because I was running into an issue where the frontend was sending double fetches - making the UI "stutter" and sending unnecessary calls to the API.
- **Router guards**: public routes never block any traffic but protected routes (like trying to view task details or the task list) will redirect to `/login` immediately if authentication is unknown. An authentication probe (`/auth/me`) runs in the background.
- **Pinia Stores**: I have several single purpose stores: `authStore`, `tasksStore`, `taskCacheStore`. Keeping these small, single purpose stores reduces coupling and makes testing easier.

### Build and Deploy Shape
- **Single deployable unit in production**: Vite outputs to `TaskMasterApi/wwwroot`. This allows the API to serve both JSON and static assets, lowering the amount of moving parts and not having to worry much about CORS. Plus, this made the Fly.io deploy configuration a little easier to manage.
