using graphnotelm.Core.Contexts.Contracts;
using graphnotelm.Core.Services;
using graphnotelm.Core.Services.Contracts;
using graphnotelm.Infrastructure.Contracts;
using graphnotelm.Infrastructure.Repository.Contracts;
using Moq;

namespace graphnotelm.Tests
{
    public class NoteGraphServiceTests
    {

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ICurrentUserContext> _currentUserMock;
        private readonly Mock<INoteGraphMetadataRepository> _noteGraphMetadataRepositoryMock;
        private readonly Mock<INoteGraphRepository> _noteGraphRepositoryMock;
        private readonly Mock<INoteGraphAccessService> _noteGraphAccessServiceMock;

        private readonly NoteGraphService testNoteGraphService;

        public NoteGraphServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _currentUserMock = new Mock<ICurrentUserContext>();
            _noteGraphMetadataRepositoryMock = new Mock<INoteGraphMetadataRepository>();
            _noteGraphRepositoryMock = new Mock<INoteGraphRepository>();
            _noteGraphAccessServiceMock = new Mock<INoteGraphAccessService>();

            testNoteGraphService = new NoteGraphService(
                _unitOfWorkMock.Object, 
                _currentUserMock.Object, 
                _noteGraphMetadataRepositoryMock.Object, 
                _noteGraphRepositoryMock.Object,
                _noteGraphAccessServiceMock.Object
            );
        }


        [Fact]
        public async Task GetGraphById_WhenGraphExists_ReturnsOk()
        {
        }

    }
}
