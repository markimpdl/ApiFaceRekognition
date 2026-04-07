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

namespace ApiFaceAi.Tests.Controllers;

public class UsersControllerTests
{
    private const string ValidFaceId = "b1c2d3e4-0000-0000-0000-000000000002";

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<IAmazonRekognition> CreateRekognitionWithExistingCollection()
    {
        var mock = new Mock<IAmazonRekognition>();
        mock.Setup(r => r.ListCollectionsAsync(It.IsAny<ListCollectionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListCollectionsResponse { CollectionIds = ["event-collection"] });
        return mock;
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
    public async Task Register_WhenPhotoIsNull_ReturnsBadRequest()
    {
        var rekognition = CreateRekognitionWithExistingCollection();
        var controller = new UsersController(CreateDb(), rekognition.Object);

        var result = await controller.Register(new RegisterUserDto { Name = "John", Photo = null! });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WhenPhotoIsEmpty_ReturnsBadRequest()
    {
        var rekognition = CreateRekognitionWithExistingCollection();
        var emptyFile = new Mock<IFormFile>();
        emptyFile.Setup(f => f.Length).Returns(0);
        var controller = new UsersController(CreateDb(), rekognition.Object);

        var result = await controller.Register(new RegisterUserDto { Name = "John", Photo = emptyFile.Object });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WhenNoFaceDetectedInPhoto_ReturnsBadRequest()
    {
        var rekognition = CreateRekognitionWithExistingCollection();
        rekognition
            .Setup(r => r.IndexFacesAsync(It.IsAny<IndexFacesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexFacesResponse { FaceRecords = [] });

        var controller = new UsersController(CreateDb(), rekognition.Object);

        var result = await controller.Register(new RegisterUserDto { Name = "John", Photo = CreateMockPhoto().Object });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WhenValidPhoto_ReturnsOkAndPersistsUser()
    {
        var db = CreateDb();
        var rekognition = CreateRekognitionWithExistingCollection();
        rekognition
            .Setup(r => r.IndexFacesAsync(It.IsAny<IndexFacesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexFacesResponse
            {
                FaceRecords = [new() { Face = new Face { FaceId = ValidFaceId } }]
            });

        var controller = new UsersController(db, rekognition.Object);

        var result = await controller.Register(new RegisterUserDto { Name = "John Doe", Photo = CreateMockPhoto().Object });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, db.Users.Count());
        Assert.Equal("John Doe", db.Users.First().Name);
        Assert.Equal(ValidFaceId, db.Users.First().RekognitionFaceId);
    }

    [Fact]
    public async Task Register_WhenCollectionDoesNotExist_CreatesCollectionBeforeIndexing()
    {
        var db = CreateDb();
        var collectionFaceId = "c1d2e3f4-0000-0000-0000-000000000003";
        var rekognition = new Mock<IAmazonRekognition>();
        rekognition
            .Setup(r => r.ListCollectionsAsync(It.IsAny<ListCollectionsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListCollectionsResponse { CollectionIds = [] });
        rekognition
            .Setup(r => r.CreateCollectionAsync(It.IsAny<CreateCollectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateCollectionResponse());
        rekognition
            .Setup(r => r.IndexFacesAsync(It.IsAny<IndexFacesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexFacesResponse
            {
                FaceRecords = [new() { Face = new Face { FaceId = collectionFaceId } }]
            });

        var controller = new UsersController(db, rekognition.Object);

        var result = await controller.Register(new RegisterUserDto { Name = "Maria", Photo = CreateMockPhoto().Object });

        Assert.IsType<OkObjectResult>(result);
        rekognition.Verify(
            r => r.CreateCollectionAsync(It.IsAny<CreateCollectionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
