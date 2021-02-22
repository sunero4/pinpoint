using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using FontAwesome5;
using Pinpoint.Core;
using Pinpoint.Core.Results;

namespace Pinpoint.Plugin.Docker
{
    public class DockerPlugin: IPlugin
    {
        private DockerClient _dockerClient;
        public PluginMeta Meta { get; set; } = new PluginMeta("Docker Plugin", PluginPriority.Highest);

        public void Load()
        {
            _dockerClient = DockerClientProvider.GetDockerClientInstance();
        }

        public void Unload() { }

        public Task<bool> Activate(Query query)
        {
            return Task.FromResult(query.RawQuery.StartsWith("docker"));
        }

        public async IAsyncEnumerable<AbstractQueryResult> Process(Query query)
        {
            var queryParts = query.RawQuery.Split(' ');
            if (query.RawQuery == "docker ps")
            {
                await foreach (var containerResult in ListContainers())
                {
                    yield return containerResult;
                }
            }

            if (query.RawQuery == "docker images")
            {
                await foreach (var imageResult in ListLocalImages())
                {
                    yield return imageResult;
                }
            }

            if (query.RawQuery.StartsWith("docker run"))
            {

                var portMappings = new List<string>();

                if (queryParts.Length > 2)
                {
                    var portsToMap = queryParts.Skip(2).Where(param => Regex.IsMatch(param, "^[0-9]+:[0-9]+$"));

                    portMappings.AddRange(portsToMap);
                }

                await foreach (var runnableImageResult in ListLocalImagesRunnable(portMappings))
                {
                    yield return runnableImageResult;
                }
            }

            if (query.RawQuery.StartsWith("docker search"))
            {
                if (queryParts.Length <= 2) yield break;

                var searchTerm = queryParts[2];

                await foreach (var imageResult in ListDockerHubImages(searchTerm))
                {
                    yield return imageResult;
                }
            }
        }

        private async IAsyncEnumerable<AbstractQueryResult> ListDockerHubImages(string searchTerm)
        {
            var images = await _dockerClient.Images.SearchImagesAsync(new ImagesSearchParameters
            {
                Term = searchTerm
            });

            foreach (var image in images)
            {
                yield return new DockerImageResult(image.Name);
            }
        }

        private async IAsyncEnumerable<AbstractQueryResult> ListDockerHubImagesRunnable(string searchTerm,
            IReadOnlyList<string> portMappings)
        {
            var images = await _dockerClient.Images.SearchImagesAsync(new ImagesSearchParameters
            {
                Term = searchTerm
            });

            foreach (var image in images)
            {
                yield return new RunnableDockerImageResult(image.Name, "", portMappings);
            }
        }

        private async IAsyncEnumerable<AbstractQueryResult> ListLocalImages()
        {
            var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters());
            foreach (var image in images)
            {
                var displayName = image.RepoTags != null && image.RepoTags.Count > 0 ? image.RepoTags[0] : image.ID;
                yield return new DockerImageResult(displayName);
            }
        }

        private async IAsyncEnumerable<AbstractQueryResult> ListLocalImagesRunnable(IReadOnlyList<string> portMappings)
        {
            var images = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters());
            foreach (var image in images)
            {
                var displayName = image.RepoTags != null && image.RepoTags.Count > 0 ? image.RepoTags[0] : image.ID;
                yield return new RunnableDockerImageResult(displayName, image.ID, portMappings);
            }
        }

        private async IAsyncEnumerable<AbstractQueryResult> ListContainers()
        {
            var statusFilter = new Dictionary<string, bool>
            {
                {"running", true}
            };

            var containers =
                await _dockerClient.Containers.ListContainersAsync(new ContainersListParameters
                {
                    Limit = 5,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        {
                            "status", statusFilter
                        }
                    }
                });

            foreach (var container in containers)
            {
                yield return new DockerContainerResult(container.Image, container.ID);
            }
        }
    }

    public class RunnableDockerImageResult: DockerImageResult
    {
        private readonly string _imageId;
        private readonly IReadOnlyList<string> _portBindings;

        public RunnableDockerImageResult(string imageName, string imageId, IReadOnlyList<string> portBindings) : base(imageName)
        {
            _imageId = imageId;
            _portBindings = portBindings;
        }

        public override void OnSelect()
        {
            var client = DockerClientProvider.GetDockerClientInstance();
            var portBindings = _portBindings.Select(portBinding =>
            {
                var ports = portBinding.Split(':');

                return new KeyValuePair<string, IList<PortBinding>>($"{ports[0]}/tcp", new List<PortBinding>
                {
                    new PortBinding
                    {
                        HostIP = "0.0.0.0",
                        HostPort = ports[1]
                    }
                });
            });

            client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = _imageId,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>(portBindings)
                }
            }).ContinueWith(async createContainerTask =>
            {
                var createdContainer = await createContainerTask;

                await client.Containers.StartContainerAsync(createdContainer.ID, null);
            });
        }
    }

    public class DockerImageResult: AbstractFontAwesomeQueryResult
    {
        public DockerImageResult(string imageName): base(imageName) { }
        public override void OnSelect()
        {
        }

        public override EFontAwesomeIcon FontAwesomeIcon => EFontAwesomeIcon.Brands_Docker;
    }

    public class DockerContainerResult: AbstractFontAwesomeQueryResult
    {
        private readonly string _containerId;
        public DockerContainerResult(string imageName, string containerId): base(imageName)
        {
            _containerId = containerId;
        }

        public override void OnSelect()
        {
            var client = DockerClientProvider.GetDockerClientInstance();

            client.Containers.StopContainerAsync(_containerId, new ContainerStopParameters());
        }

        public override EFontAwesomeIcon FontAwesomeIcon => EFontAwesomeIcon.Brands_Docker;
    }

    public class DockerClientProvider
    {
        private static DockerClient _dockerClient;

        public static DockerClient GetDockerClientInstance()
        {
            return _dockerClient ??= new DockerClientConfiguration().CreateClient();
        }
    }
}
