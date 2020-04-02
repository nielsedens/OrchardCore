using System.Threading.Tasks;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Tests.Apis.Context;
using OrchardCore.Environment.Shell;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using OrchardCore.ContentManagement.Records;
using System.Linq;
using OrchardCore.Taxonomies.Fields;
using YesSql.Services;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Autoroute.Drivers;

namespace OrchardCore.Tests.Apis.ContentManagement.ContentApiController
{
    public class BlogPostApiControllerTests
    {
        [Fact]
        public async Task ShouldCreateDraftOfExistingContentItem()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                await context.InitializeAsync();

                context.BlogPost.Latest = false;
                context.BlogPost.Published = true; // Deliberately set these incorrectly.

                // Act
                var content = await context.Client.PostAsJsonAsync("api/content?draft=true", context.BlogPost);
                var draftContentItem = await content.Content.ReadAsAsync<ContentItem>();

                // Test
                Assert.True(draftContentItem.Latest);
                Assert.False(draftContentItem.Published);
            }
        }

        [Fact]
        public async Task ShouldCreateAndPublishExistingContentItem()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                await context.InitializeAsync();

                context.BlogPost.Latest = false;
                context.BlogPost.Published = false; // Deliberately set these incorrectly.

                // Act
                var content = await context.Client.PostAsJsonAsync("api/content", context.BlogPost);
                var draftContentItem = await content.Content.ReadAsAsync<ContentItem>();

                // Test
                Assert.True(draftContentItem.Latest);
                Assert.True(draftContentItem.Published);
            }
        }

        [Fact]
        public async Task ShouldOnlyCreateTwoContentItemRecordsForExistingContentItem()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                await context.InitializeAsync();

                context.BlogPost.Latest = false;
                context.BlogPost.Published = false; // Deliberately set these incorrectly.

                // Act
                await context.Client.PostAsJsonAsync("api/content", context.BlogPost);

                // Test
                using (var shellScope = await BlogPostApiControllerContext.ShellHost.GetScopeAsync(context.TenantName))
                {
                    await shellScope.UsingAsync(async scope =>
                    {
                        var session = scope.ServiceProvider.GetRequiredService<ISession>();
                        var blogPosts = await session.Query<ContentItem, ContentItemIndex>(x =>
                            x.ContentType == "BlogPost").ListAsync();

                        Assert.Equal(2, blogPosts.Count());
                    });
                }
            }
        }

        [Fact]
        public async Task ShouldCreateDraftOfNewContentItem()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                var displayText = "some other blog post";
                await context.InitializeAsync();

                var contentItem = new ContentItem
                {
                    ContentType = "BlogPost",
                    DisplayText = displayText,
                    Latest = true,
                    Published = true // Deliberately set these values incorrectly
                };

                contentItem
                    .Weld(new AutoroutePart
                    {
                        Path = "Path2"
                    });

                contentItem
                    .Weld(new ContainedPart
                    {
                        ListContentItemId = context.BlogContentItemId
                    });

                var blogFields = new ContentPart();
                blogFields
                    .Weld("Categories", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.CategoriesTaxonomyContentItemId
                    });

                blogFields
                    .Weld("Tags", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.TagsTaxonomyContentItemId
                    });

                contentItem
                    .Weld("BlogPost", blogFields);

                // Act
                var content = await context.Client.PostAsJsonAsync("api/content?draft=true", contentItem);
                var draftContentItem = await content.Content.ReadAsAsync<ContentItem>();

                // Test
                Assert.True(draftContentItem.Latest);
                Assert.False(draftContentItem.Published);
                Assert.Equal(displayText, draftContentItem.DisplayText);
                Assert.NotNull(draftContentItem.As<AutoroutePart>());
            }
        }

        [Fact]
        public async Task ShouldCreateAndPublishNewContentItem()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                var displayText = "some other blog post";
                var path = "path2";
                await context.InitializeAsync();

                var contentItem = new ContentItem
                {
                    ContentType = "BlogPost",
                    DisplayText = displayText,
                    Latest = false,
                    Published = false // Deliberately set these values incorrectly
                };

                contentItem
                    .Weld(new AutoroutePart
                    {
                        Path = path
                    });

                contentItem
                    .Weld(new ContainedPart
                    {
                        ListContentItemId = context.BlogContentItemId
                    });

                var blogFields = new ContentPart();
                blogFields
                    .Weld("Categories", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.CategoriesTaxonomyContentItemId
                    });

                blogFields
                    .Weld("Tags", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.TagsTaxonomyContentItemId
                    });

                contentItem
                    .Weld("BlogPost", blogFields);

                // Act
                var content = await context.Client.PostAsJsonAsync("api/content", contentItem);
                var publishedContentItem = await content.Content.ReadAsAsync<ContentItem>();

                // Test
                Assert.True(publishedContentItem.Latest);
                Assert.True(publishedContentItem.Published);
                Assert.Equal(displayText, publishedContentItem.DisplayText);
                Assert.Equal(path, publishedContentItem.As<AutoroutePart>()?.Path);
            }
        }

        [Fact]
        public async Task ShouldFailValidationWhenAutoroutePathIsNotUnique()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                await context.InitializeAsync();

                var contentItem = new ContentItem
                {
                    ContentType = "BlogPost",
                    DisplayText = "some other blog post",
                    Latest = false,
                    Published = false // Deliberately set these values incorrectly
                };

                contentItem
                    .Weld(new AutoroutePart
                    {
                        Path = "blog/post-1" // Deliberately set to an existing path.
                    });

                contentItem
                    .Weld(new ContainedPart
                    {
                        ListContentItemId = context.BlogContentItemId
                    });

                var blogFields = new ContentPart();
                blogFields
                    .Weld("Categories", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.CategoriesTaxonomyContentItemId
                    });

                blogFields
                    .Weld("Tags", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.TagsTaxonomyContentItemId
                    });

                contentItem
                    .Weld("BlogPost", blogFields);

                // Act
                var result = await context.Client.PostAsJsonAsync("api/content", contentItem);
                var problemDetails = await result.Content.ReadAsAsync<ProblemDetails>();

                // Test
                Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
                Assert.Contains(AutoroutePartDisplay.DefaultUniquePathError, problemDetails.Detail);
                using (var shellScope = await BlogPostApiControllerContext.ShellHost.GetScopeAsync(context.TenantName))
                {
                    await shellScope.UsingAsync(async scope =>
                    {
                        var session = scope.ServiceProvider.GetRequiredService<ISession>();
                        var blogPosts = await session.Query<ContentItem, ContentItemIndex>(x =>
                            x.ContentType == "BlogPost").ListAsync();
                         
                        Assert.Single(blogPosts);
                    });
                }

            }
        }

        [Fact]
        public async Task ShouldGenerateUniqueAutoroutePath()
        {
            using (var context = new BlogPostApiControllerContext())
            {
                // Setup
                await context.InitializeAsync();

                var contentItem = new ContentItem
                {
                    ContentType = "BlogPost",
                    DisplayText = "some other blog post",
                    Latest = false,
                    Published = false // Deliberately set these values incorrectly
                };

                contentItem
                    .Weld(new ContainedPart
                    {
                        ListContentItemId = context.BlogContentItemId
                    });

                var blogFields = new ContentPart();
                blogFields
                    .Weld("Categories", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.CategoriesTaxonomyContentItemId
                    });

                blogFields
                    .Weld("Tags", new TaxonomyField
                    {
                        TaxonomyContentItemId = context.TagsTaxonomyContentItemId
                    });

                contentItem
                    .Weld("BlogPost", blogFields);

                // Act
                var content = await context.Client.PostAsJsonAsync("api/content", contentItem);
                var publishedContentItem = await content.Content.ReadAsAsync<ContentItem>();

                // Test
                using (var shellScope = await BlogPostDeploymentContext.ShellHost.GetScopeAsync(context.TenantName))
                {
                    var blogPostContentItemIds = new List<string>
                    {
                        context.BlogPost.ContentItemId,
                        publishedContentItem.ContentItemId
                    };

                    await shellScope.UsingAsync(async scope =>
                    {
                        var session = scope.ServiceProvider.GetRequiredService<ISession>();
                        var newAutoroutePartIndex = await session
                            .QueryIndex<AutoroutePartIndex>(x => x.ContentItemId == publishedContentItem.ContentItemId)
                            .FirstOrDefaultAsync();

                        // The Autoroute part was not welded on, so ContentManager.NewAsync should add it
                        // with an empty path and then generate a unique path from the liquid pattern.
                        Assert.Equal("blog/some-other-blog-post", publishedContentItem.As<AutoroutePart>().Path);
                    });
                }
            }
        }
    }
}