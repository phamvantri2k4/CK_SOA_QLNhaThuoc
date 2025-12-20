using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DrugService.Data;
using DrugService.Models;

namespace DrugService.Controllers
{
    /// <summary>
    /// Controller quản lý thông tin thuốc (Drug)
    /// Cung cấp các API CRUD cơ bản cho Drug
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DrugsController : ControllerBase
    {
        private readonly DrugDbContext _context;
        private readonly ILogger<DrugsController> _logger;

        public DrugsController(DrugDbContext context, ILogger<DrugsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách tất cả các thuốc
        /// GET /api/drugs
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Drug>>> GetDrugs()
        {
            try
            {
                var drugs = await _context.Drugs.ToListAsync();
                _logger.LogInformation($"Trả về danh sách {drugs.Count} thuốc");
                return Ok(drugs);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách thuốc: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy danh sách thuốc" });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết một thuốc theo ID
        /// GET /api/drugs/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Drug>> GetDrug(int id)
        {
            try
            {
                var drug = await _context.Drugs.FindAsync(id);

                if (drug == null)
                {
                    _logger.LogWarning($"Không tìm thấy thuốc với ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy thuốc với ID: {id}" });
                }

                _logger.LogInformation($"Trả về thông tin thuốc ID: {id}, Name: {drug.Name}");
                return Ok(drug);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy thông tin thuốc ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi lấy thông tin thuốc" });
            }
        }

        /// <summary>
        /// Thêm thuốc mới
        /// POST /api/drugs
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Drug>> CreateDrug([FromBody] Drug drug)
        {
            try
            {
                // Validate dữ liệu
                if (string.IsNullOrWhiteSpace(drug.Name))
                {
                    return BadRequest(new { message = "Tên thuốc không được để trống" });
                }

                if (drug.SellPrice < 0)
                {
                    return BadRequest(new { message = "Giá bán phải lớn hơn hoặc bằng 0" });
                }

                if (drug.BoxPrice < 0)
                {
                    return BadRequest(new { message = "Giá bán theo hộp phải lớn hơn hoặc bằng 0" });
                }

                if (drug.PackSize <= 0)
                {
                    return BadRequest(new { message = "PackSize phải lớn hơn 0" });
                }

                // Thêm vào database
                _context.Drugs.Add(drug);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã thêm thuốc mới: ID={drug.Id}, Name={drug.Name}");

                // Trả về 201 Created với location header
                return CreatedAtAction(nameof(GetDrug), new { id = drug.Id }, drug);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi thêm thuốc: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi thêm thuốc" });
            }
        }

        /// <summary>
        /// Cập nhật thông tin thuốc
        /// PUT /api/drugs/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDrug(int id, [FromBody] Drug drug)
        {
            // Kiểm tra ID có khớp không
            if (id != drug.Id)
            {
                return BadRequest(new { message = "ID không khớp" });
            }

            try
            {
                // Kiểm tra thuốc có tồn tại không
                var existingDrug = await _context.Drugs.FindAsync(id);
                if (existingDrug == null)
                {
                    _logger.LogWarning($"Không tìm thấy thuốc với ID: {id} để cập nhật");
                    return NotFound(new { message = $"Không tìm thấy thuốc với ID: {id}" });
                }

                // Validate dữ liệu
                if (string.IsNullOrWhiteSpace(drug.Name))
                {
                    return BadRequest(new { message = "Tên thuốc không được để trống" });
                }

                // Cập nhật thông tin
                existingDrug.Name = drug.Name;
                existingDrug.Code = drug.Code;
                existingDrug.Category = drug.Category;
                existingDrug.Unit = drug.Unit;
                existingDrug.PackSize = drug.PackSize <= 0 ? 1 : drug.PackSize;
                existingDrug.ImportPrice = drug.ImportPrice;
                existingDrug.SellPrice = drug.SellPrice;
                existingDrug.BoxPrice = drug.BoxPrice;
                if (!string.IsNullOrEmpty(drug.ImageUrl))
                {
                    existingDrug.ImageUrl = drug.ImageUrl;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã cập nhật thuốc: ID={id}, Name={drug.Name}");

                return Ok(existingDrug);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi cập nhật thuốc ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi cập nhật thuốc" });
            }
        }

        /// <summary>
        /// Xóa thuốc
        /// DELETE /api/drugs/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDrug(int id)
        {
            try
            {
                var drug = await _context.Drugs.FindAsync(id);
                
                if (drug == null)
                {
                    _logger.LogWarning($"Không tìm thấy thuốc với ID: {id} để xóa");
                    return NotFound(new { message = $"Không tìm thấy thuốc với ID: {id}" });
                }

                _context.Drugs.Remove(drug);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Đã xóa thuốc: ID={id}, Name={drug.Name}");

                return Ok(new { message = $"Đã xóa thuốc '{drug.Name}' thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi xóa thuốc ID {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi server khi xóa thuốc" });
            }
        }
    }
}

