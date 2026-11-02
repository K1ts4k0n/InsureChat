using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Admin;
using InsureChatBot.Api.Services.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    /// <summary>หลังบ้าน /admin — ดูบทสนทนา/คำสั่งซื้อ/คลังความรู้ (ADR 0016 Phase 3). admin เท่านั้น.</summary>
    [Authorize(Roles = UserRoles.Admin)]
    [Route("api/Admin")]
    [ApiController]
    public class AdminDataController : ControllerBase
    {
        private readonly IAdminQueryService _query;
        private readonly IKnowledgeStore _knowledge;

        public AdminDataController(IAdminQueryService query, IKnowledgeStore knowledge)
        {
            _query = query;
            _knowledge = knowledge;
        }

        [HttpGet("Conversations")]
        public IActionResult Conversations([FromQuery] int take = 50, [FromQuery] int skip = 0, [FromQuery] string q = null)
            => Ok(_query.ListConversations(take, skip, q));

        [HttpGet("Conversations/{id:long}")]
        public IActionResult Conversation(long id)
        {
            var d = _query.GetConversation(id);
            return d == null ? NotFound(new { message = "ไม่พบบทสนทนา" }) : Ok(d);
        }

        [HttpGet("Orders")]
        public IActionResult Orders([FromQuery] int take = 50, [FromQuery] int skip = 0)
            => Ok(_query.ListOrders(take, skip));

        [HttpGet("Knowledge")]
        public IActionResult Knowledge()
            => Ok(_knowledge.List().Select(a => new { a.Slug, a.Title, a.Type, a.IsInjected, a.UpdatedAt }));

        [HttpGet("Knowledge/Article")]
        public IActionResult KnowledgeArticle([FromQuery] string slug)
        {
            var a = _knowledge.Get(slug);
            return a == null ? NotFound(new { message = "ไม่พบบทความ" })
                : Ok(new { a.Slug, a.Title, a.Type, a.Content, a.Tags, a.Aliases, a.IsInjected, a.UpdatedAt });
        }

        [HttpPost("Knowledge/Sync")]
        public IActionResult SyncKnowledge() => Ok(new { synced = _knowledge.Sync() });
    }
}
