using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GazeteDagitim.Tests;

public sealed class PwaTests : IClassFixture<GazeteWebFactory>
{
    private readonly HttpClient _client;

    public PwaTests(GazeteWebFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task MainLayout_DeclaresInstallableAppMetadata()
    {
        var html = await _client.GetStringAsync("/menu");

        Assert.Contains("rel=\"manifest\" href=\"/manifest.webmanifest\"", html);
        Assert.Contains("rel=\"apple-touch-icon\" href=\"/icons/apple-touch-icon.png\"", html);
        Assert.Contains("name=\"apple-mobile-web-app-capable\" content=\"yes\"", html);
        Assert.Contains("data-pwa-panel", html);
        Assert.Contains("data-pwa-primary", html);
        Assert.Contains("data-offline-banner", html);
    }

    [Fact]
    public async Task Manifest_DefinesStandaloneTurkishApplication()
    {
        var response = await _client.GetAsync("/manifest.webmanifest");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var manifest = await JsonDocument.ParseAsync(stream);
        var root = manifest.RootElement;

        Assert.Equal("/", root.GetProperty("id").GetString());
        Assert.Equal("/menu", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.Equal("tr", root.GetProperty("lang").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("#050506", root.GetProperty("background_color").GetString());
        Assert.Equal("#a9003b", root.GetProperty("theme_color").GetString());
        Assert.Contains(
            root.GetProperty("icons").EnumerateArray(),
            icon => icon.GetProperty("purpose").GetString() == "maskable");
    }

    [Theory]
    [InlineData("/icons/icon-192.png", 192, 192)]
    [InlineData("/icons/icon-512.png", 512, 512)]
    [InlineData("/icons/icon-maskable-512.png", 512, 512)]
    [InlineData("/icons/apple-touch-icon.png", 180, 180)]
    public async Task AppIcons_ArePngFilesWithDeclaredSize(
        string url,
        int expectedWidth,
        int expectedHeight)
    {
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length >= 24);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal(expectedWidth, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    [Fact]
    public async Task ServiceWorker_IsFreshAndDoesNotCacheBusinessRequests()
    {
        var response = await _client.GetAsync("/service-worker.js");

        response.EnsureSuccessStatusCode();
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("/", response.Headers.GetValues("Service-Worker-Allowed").Single());

        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("request.method !== \"GET\"", script);
        Assert.Contains("request.mode === \"navigate\"", script);
        Assert.Contains("fetch(request).catch(() => caches.match(OFFLINE_URL))", script);
        Assert.DoesNotContain("/deliveries", script);
        Assert.DoesNotContain("/payments", script);
    }

    [Fact]
    public async Task OfflinePage_IsAvailable()
    {
        var response = await _client.GetAsync("/offline.html");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("İnternet bağlantısı yok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AndroidUpdateManifest_IsFreshAndHasTrustedReleaseMetadata()
    {
        var response = await _client.GetAsync("/downloads/android/update.json");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var manifest = await JsonDocument.ParseAsync(stream);
        var root = manifest.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("com.turnaexpress.gazete", root.GetProperty("packageName").GetString());
        Assert.True(root.GetProperty("versionCode").GetInt32() > 0);
        Assert.Matches(@"^[a-f0-9]{64}$", root.GetProperty("sha256").GetString()!);
        Assert.InRange(root.GetProperty("size").GetInt64(), 1, 200L * 1024 * 1024);

        var apkUrl = new Uri(root.GetProperty("apkUrl").GetString()!);
        Assert.Equal(Uri.UriSchemeHttps, apkUrl.Scheme);
        Assert.Equal("gazete.turnaexpress.com.tr", apkUrl.Host);
        Assert.EndsWith(".apk", apkUrl.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientGuard_DisablesMutatingActionsWhileOffline()
    {
        var script = await _client.GetStringAsync("/js/site.js");

        Assert.Contains("!navigator.onLine", script);
        Assert.Contains("form[method=\"post\" i], form[data-autosave-url]", script);
        Assert.Contains("event.stopImmediatePropagation()", script);
        Assert.Contains("Kayıt işlemleri bağlantı gelene kadar kullanılamaz",
            await _client.GetStringAsync("/menu"));
    }
}
