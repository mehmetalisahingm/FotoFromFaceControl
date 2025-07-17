using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    [HttpGet("list")]
    public IActionResult ListFiles()
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
            return Ok(Array.Empty<string>());

        var files = Directory.GetFiles(uploadsFolder)
                             .Select(Path.GetFileName)
                             .ToArray();

        return Ok(files);
    }
}
