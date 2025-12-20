using DrugService.Data;
using DrugService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DrugService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly DrugDbContext _context;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(DrugDbContext context, ILogger<CategoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách danh mục: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy danh sách danh mục" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { message = "Không tìm thấy danh mục" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh mục #{id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy danh mục" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<Category>> Create([FromBody] Category category)
        {
            try
            {
                var name = category?.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Tên danh mục không được để trống" });
                }

                var exists = await _context.Categories.AnyAsync(c => c.Name == name);
                if (exists)
                {
                    return Conflict(new { message = "Danh mục đã tồn tại" });
                }

                var entity = new Category { Name = name };
                _context.Categories.Add(entity);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = entity.Id }, entity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi tạo danh mục: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi tạo danh mục" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Category category)
        {
            try
            {
                var name = category?.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { message = "Tên danh mục không được để trống" });
                }

                var entity = await _context.Categories.FindAsync(id);
                if (entity == null)
                {
                    return NotFound(new { message = "Không tìm thấy danh mục" });
                }

                var exists = await _context.Categories.AnyAsync(c => c.Id != id && c.Name == name);
                if (exists)
                {
                    return Conflict(new { message = "Danh mục đã tồn tại" });
                }

                entity.Name = name;
                await _context.SaveChangesAsync();

                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi cập nhật danh mục: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi cập nhật danh mục" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var entity = await _context.Categories.FindAsync(id);
                if (entity == null)
                {
                    return NotFound(new { message = "Không tìm thấy danh mục" });
                }

                var isUsed = await _context.Drugs.AnyAsync(d => d.Category == entity.Name);
                if (isUsed)
                {
                    return Conflict(new { message = "Danh mục đang được sử dụng bởi thuốc, không thể xóa" });
                }

                _context.Categories.Remove(entity);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đã xóa danh mục" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa danh mục: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi xóa danh mục" });
            }
        }
    }
}
