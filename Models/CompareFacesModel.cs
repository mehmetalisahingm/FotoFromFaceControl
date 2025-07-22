namespace FotoFromFaceControl.Models
{
    public class CompareFacesModel
    {
        public IFormFile File1 { get; set; } = null!;
        public IFormFile File2 { get; set; } = null!;
    }
}
