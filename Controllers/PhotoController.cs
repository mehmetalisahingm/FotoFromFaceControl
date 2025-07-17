//using Microsoft.AspNetCore.Mvc;

//namespace FotoFromFaceControl.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class PhotoController : ControllerBase
//    {
//        [HttpPost("upload")]
//        [Consumes("multipart/form-data")] 
//        public async Task<IActionResult> UploadPhoto(IFormFile photo)
//        {
//            if (photo == null || photo.Length == 0)
//                return BadRequest("Fotoğraf yüklenmedi.");

//            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
//            if (!Directory.Exists(uploadsFolder))
//                Directory.CreateDirectory(uploadsFolder);

//            var uniqueFileName = $"{Guid.NewGuid()}_{photo.FileName}";
//            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                await photo.CopyToAsync(stream);
//            }

//            var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";

//            return Ok(new
//            {
//                message = "Fotoğraf yüklendi.",
//                url = fileUrl
//            });
//        }

//    }
//}
