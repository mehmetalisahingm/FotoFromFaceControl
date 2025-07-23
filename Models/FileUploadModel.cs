using Microsoft.AspNetCore.Http;

namespace FotoFromFaceControl.Models
{
    public class FileUploadModel
    {
        public IFormFile File { get; set; } = null!;

        public bool CropEyes { get; set; } = true;

        public bool CropNose { get; set; } = true;

        public bool CropFace { get; set; } = true;
    }
}
