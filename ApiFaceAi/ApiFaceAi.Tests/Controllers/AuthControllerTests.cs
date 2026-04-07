using Xunit;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using ApiFaceAi.Controllers;
using ApiFaceAi.Data;
using ApiFaceAi.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using DbUser = ApiFaceAi.Models.User;

namespace ApiFaceAi.Tests.Controllers;

public class AuthControllerTests
{
    private const string ValidFaceId = "a1b2c3d4-0000-0000-0000-000000000001";

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<IFormFile> CreateMockPhoto()
    {
        var content = new byte[] { 1, 2, 3 };
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, _) => new MemoryStream(content).CopyTo(s))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task Login_WhenPhotoIsNull_ReturnsBadRequest()
    {
        var controller = new AuthController(CreateDb(), new Mock<IAmazonRekognition>().Object);

        var result = await controller.Login(new LoginDto { Photo = null! });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenPhotoIsEmpty_ReturnsBadRequest()
    {
        var emptyFile = new Mock<IFormFile>();
        emptyFile.Setup(f => f.Length).Returns(0);
        var controller = new AuthController(CreateDb(), new Mock<IAmazonRekognition>().Object);

        var result = await controller.Login(new LoginDto { Photo = emptyFile.Object });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenNoFaceMatchFound_ReturnsUnauthorized()
    {
        var rekognition = new Mock<IAmazonRekognition>();
        rekognition
            .Setup(r => r.SearchFacesByImageAsync(It.IsAny<SearchFacesByImageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchFacesByImageResponse { FaceMatches = [] });

        var controller = new AuthController(CreateDb(), rekognition.Object);

        var result = await controller.Login(new LoginDto { Photo = CreateMockPhoto().Object });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenFaceMatchButUserNotInDatabase_ReturnsUnauthorized()
    {
        var rekognition = new Mock<IAmazonRekognition>();
        rekognition
            .Setup(r => r.SearchFacesByImageAsync(It.IsAny<SearchFacesByImageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchFacesByImageResponse
            {
                FaceMatches =
                [
                    new() { Face = new Face { FaceId = ValidFaceId }, Similarity = 98f }
                ]
            });

        var controller = new AuthController(CreateDb(), rekognition.Object);

        var result = await controller.Login(new LoginDto { Photo = CreateMockPhoto().Object });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenFaceMatchAndUserExists_ReturnsOkWithUserData()
    {
        var db = CreateDb();
        db.Users.Add(new DbUser { Name = "John Doe", RekognitionFaceId = ValidFaceId, PhotoBlob = [1, 2, 3] });
        await db.SaveChangesAsync();

        var rekognition = new Mock<IAmazonRekognition>();
        rekognition
            .Setup(r => r.SearchFacesByImageAsync(It.IsAny<SearchFacesByImageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchFacesByImageResponse
            {
                FaceMatches =
                [
                    new() { Face = new Face { FaceId = ValidFaceId }, Similarity = 98f }
                ]
            });

        var controller = new AuthController(db, rekognition.Object);

        var result = await controller.Login(new LoginDto { Photo = CreateMockPhoto().Object });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenRekognitionThrowsException_Returns500()
    {
        var rekognition = new Mock<IAmazonRekognition>();
        rekognition
            .Setup(r => r.SearchFacesByImageAsync(It.IsAny<SearchFacesByImageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AWS connection error"));

        var controller = new AuthController(CreateDb(), rekognition.Object);

        var result = await controller.Login(new LoginDto { Photo = CreateMockPhoto().Object });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
