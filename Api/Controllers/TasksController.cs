using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger<TasksController> _logger;

        public TasksController(Microsoft.Extensions.Logging.ILogger<TasksController> logger)
        {
            _logger = logger;
        }
        // Manager or Admin creates a task assigned to a user
        [HttpPost("tasks")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> CreateTask([FromBody] Api.Dtos.TaskCreateRequest req,
            [FromServices] Api.Repositories.ITaskRepository taskRepo,
            [FromServices] Api.Repositories.IUserRepository userRepo)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.AssigneeUsername))
                return BadRequest();
            var creatorLog = User?.Identity?.Name ?? "(anonymous)";
            // Sanitize inputs for both storage and logging a safe, encoded form
            var assignee = Api.Utilities.InputValidation.Sanitize(req.AssigneeUsername).Trim();
            var sanitizedTitleForLog = Api.Utilities.ValidationHelpers.Sanitize(req.Title);
            _logger.LogInformation("CreateTask invoked by '{Creator}' for assignee '{Assignee}' title:'{Title}'", Api.Utilities.UsernameSanitizer.SanitizeUsername(creatorLog), assignee, sanitizedTitleForLog.Length > 200 ? sanitizedTitleForLog.Substring(0, 200) : sanitizedTitleForLog);

            var exists = await userRepo.GetHashedPasswordAsync(assignee);
            if (string.IsNullOrEmpty(exists))
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "assignee does not exist" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
            }

            var creator = User?.Identity?.Name ?? string.Empty;
            var item = new Api.Models.TaskItem
            {
                // Titles are treated as short display text; sanitize for HTML to avoid
                // accidental markup execution if a client renders the field as HTML.
                Title = Api.Utilities.HtmlSanitizer.SanitizeForHtml(req.Title).Trim(),
                // Sanitize free-text descriptions for HTML rendering to prevent XSS when
                // clients render description content. Use HtmlSanitizer which removes
                // script tags, event handlers, javascript: URIs and encodes the result.
                Description = string.IsNullOrWhiteSpace(req.Description) ? null : Api.Utilities.HtmlSanitizer.SanitizeForHtml(req.Description).Trim(),
                CreatorUsername = creator,
                AssigneeUsername = assignee
            };

            var saved = await taskRepo.AddTaskAsync(item);
            var dto = new Api.Dtos.TaskDto
            {
                Id = saved.Id,
                Title = saved.Title,
                Description = saved.Description,
                Creator = saved.CreatorUsername,
                Assignee = saved.AssigneeUsername,
                Completed = saved.Completed,
                CreatedAt = saved.CreatedAt,
                CompletedAt = saved.CompletedAt
            };
            // Avoid framework JSON output formatter for test-host compatibility; set Location and
            // return serialized JSON explicitly. Guard Response in case controller is invoked
            // directly in unit tests where an HttpContext/Response may not be present.
            if (HttpContext?.Response?.Headers != null)
            {
                HttpContext.Response.Headers["Location"] = $"/tasks/{dto.Id}";
            }
            return new ContentResult { Content = System.Text.Json.JsonSerializer.Serialize(dto), ContentType = "application/json", StatusCode = 201 };
        }

        // User creates a task assigned to themselves
        [HttpPost("tasks/self")]
        [Authorize]
        public async Task<IActionResult> CreateSelfTask([FromBody] Api.Dtos.TaskCreateRequest req,
            [FromServices] Api.Repositories.ITaskRepository taskRepo)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Title)) return BadRequest();
            var username = User?.Identity?.Name ?? string.Empty;
            _logger.LogInformation("CreateSelfTask invoked by '{User}'", username);
            var item = new Api.Models.TaskItem
            {
                Title = Api.Utilities.HtmlSanitizer.SanitizeForHtml(req.Title).Trim(),
                Description = string.IsNullOrWhiteSpace(req.Description) ? null : Api.Utilities.HtmlSanitizer.SanitizeForHtml(req.Description).Trim(),
                CreatorUsername = username,
                AssigneeUsername = username
            };
            var saved = await taskRepo.AddTaskAsync(item);
            var dto = new Api.Dtos.TaskDto
            {
                Id = saved.Id,
                Title = saved.Title,
                Description = saved.Description,
                Creator = saved.CreatorUsername,
                Assignee = saved.AssigneeUsername,
                Completed = saved.Completed,
                CreatedAt = saved.CreatedAt,
                CompletedAt = saved.CompletedAt
            };
            if (HttpContext?.Response?.Headers != null)
            {
                HttpContext.Response.Headers["Location"] = $"/tasks/{dto.Id}";
            }
            return new ContentResult { Content = System.Text.Json.JsonSerializer.Serialize(dto), ContentType = "application/json", StatusCode = 201 };
        }

        // Get tasks assigned to current user
        [HttpGet("tasks")]
        [Authorize]
        public async Task<IActionResult> GetMyTasks([FromServices] Api.Repositories.ITaskRepository taskRepo)
        {
            var username = User?.Identity?.Name ?? string.Empty;
            _logger.LogInformation("GetMyTasks requested by '{User}'", username);
            var tasks = await taskRepo.GetTasksForUserAsync(username);
            var payload = System.Text.Json.JsonSerializer.Serialize(tasks);
            return Content(payload, "application/json");
        }

        // Manager or Admin: get tasks for a managed user
        [HttpGet("manager/users/{username}/tasks")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetTasksForUser(string username,
            [FromServices] Api.Repositories.ITaskRepository taskRepo,
            [FromServices] Api.Repositories.IUserRepository userRepo)
        {
            var requester = User?.Identity?.Name ?? string.Empty;
            var roles = User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray() ?? System.Array.Empty<string>();
            _logger.LogInformation("GetTasksForUser requested by '{Requester}' for target '{Target}' (roles={Roles})", requester, username, string.Join(',', roles));
            var isAdmin = roles.Any(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));
            if (!isAdmin)
            {
                // verify manager relationship
                var managed = await userRepo.GetUsersForManagerAsync(requester);
                if (managed == null || !managed.Contains(username, System.StringComparer.OrdinalIgnoreCase)) return Forbid();
            }

            var tasks = await taskRepo.GetTasksForUserAsync(username);
            var payload = System.Text.Json.JsonSerializer.Serialize(tasks);
            return Content(payload, "application/json");
        }

        // Return list of users managed by the caller (Manager) or all users for Admin
        [HttpGet("manager/users")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetManagedUsers([FromServices] Api.Repositories.IUserRepository userRepo)
        {
            var requester = User?.Identity?.Name ?? string.Empty;
            var roles = User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray() ?? System.Array.Empty<string>();
            _logger.LogInformation("GetManagedUsers requested by '{Requester}' (roles={Roles})", requester, string.Join(',', roles));
            var isAdmin = roles.Any(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));
            if (isAdmin)
            {
                var all = await userRepo.GetAllUsersAsync();
                var payload = System.Text.Json.JsonSerializer.Serialize(all.Keys.ToArray());
                return Content(payload, "application/json");
            }

            var managed = await userRepo.GetUsersForManagerAsync(requester);
            var payload2 = System.Text.Json.JsonSerializer.Serialize(managed ?? System.Array.Empty<string>());
            return Content(payload2, "application/json");
        }

        // Complete a task
        [HttpPatch("tasks/{id}/complete")]
        [Authorize]
        public async Task<IActionResult> CompleteTask(int id,
            [FromServices] Api.Repositories.ITaskRepository taskRepo,
            [FromServices] Api.Repositories.IUserRepository userRepo)
        {
            var requester = User?.Identity?.Name ?? string.Empty;
            var task = await taskRepo.GetTaskByIdAsync(id);
            if (task == null) return NotFound();

            var roles = User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray() ?? System.Array.Empty<string>();
            var isAdmin = roles.Any(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));

            if (string.Equals(task.AssigneeUsername, requester, System.StringComparison.OrdinalIgnoreCase))
            {
                // allowed
            }
            else if (isAdmin)
            {
                // allowed
            }
            else
            {
                // check if requester is manager of the assignee
                var managed = await userRepo.GetUsersForManagerAsync(requester);
                if (managed == null || !managed.Contains(task.AssigneeUsername, System.StringComparer.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var ok = await taskRepo.MarkCompleteAsync(id, requester);
            if (!ok) return BadRequest();
            return Ok();
        }

        // Mark a task as in-progress (started)
        [HttpPatch("tasks/{id}/start")]
        [Authorize]
        public async Task<IActionResult> StartTask(int id,
            [FromServices] Api.Repositories.ITaskRepository taskRepo,
            [FromServices] Api.Repositories.IUserRepository userRepo)
        {
            var requester = User?.Identity?.Name ?? string.Empty;
            var task = await taskRepo.GetTaskByIdAsync(id);
            if (task == null) return NotFound();

            var roles = User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray() ?? System.Array.Empty<string>();
            var isAdmin = roles.Any(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));

            if (string.Equals(task.AssigneeUsername, requester, System.StringComparison.OrdinalIgnoreCase))
            {
                // assignee can start
            }
            else if (isAdmin)
            {
                // admin can start
            }
            else
            {
                // check if requester is manager of the assignee
                var managed = await userRepo.GetUsersForManagerAsync(requester);
                if (managed == null || !managed.Contains(task.AssigneeUsername, System.StringComparer.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var ok = await taskRepo.MarkInProgressAsync(id, requester);
            if (!ok) return BadRequest();
            return Ok();
        }

        // Reassign a task to another user (Manager or Admin)
        [HttpPost("tasks/{id}/reassign")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> ReassignTask(int id, [FromBody] System.Collections.Generic.Dictionary<string, string?> body,
            [FromServices] Api.Repositories.ITaskRepository taskRepo,
            [FromServices] Api.Repositories.IUserRepository userRepo)
        {
            if (body == null || !body.TryGetValue("assignee", out var assignee) || string.IsNullOrWhiteSpace(assignee)) return BadRequest();
            assignee = Api.Utilities.InputValidation.Sanitize(assignee).Trim();

            var requester = User?.Identity?.Name ?? string.Empty;
            var roles = User?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray() ?? System.Array.Empty<string>();
            var isAdmin = roles.Any(r => string.Equals(r, "Admin", System.StringComparison.OrdinalIgnoreCase));

            var task = await taskRepo.GetTaskByIdAsync(id);
            if (task == null) return NotFound();

            if (!isAdmin)
            {
                // verify manager relationship: requester must manage current assignee
                var managed = await userRepo.GetUsersForManagerAsync(requester);
                if (managed == null || !managed.Contains(task.AssigneeUsername, System.StringComparer.OrdinalIgnoreCase)) return Forbid();
            }

            // ensure new assignee exists
            var exists = await userRepo.GetHashedPasswordAsync(assignee);
            if (string.IsNullOrEmpty(exists))
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "assignee does not exist" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
            }

            var ok = await taskRepo.ReassignTaskAsync(id, assignee, requester);
            if (!ok) return BadRequest();
            return Ok();
        }
    }
}
