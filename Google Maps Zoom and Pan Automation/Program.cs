using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    // Define boundary coordinates for better focus on the USA
    private static readonly (double lat, double lng)[] Boundaries = new (double, double)[]
    {
        (44.987931, -66.9010417),
        (42.7609504, -78.9952792),
        (48.0338732, -88.8417626),
        (48.7043183, -99.9140229),
        (49.2334375, -114.9865388),
        (47.8547471, -123.2837118),
        (40.9885808, -122.3273668),
        (34.2053609, -119.5019602),
        (31.3748893, -111.2504067),
        (28.7839807, -103.5092634),
        (26.1825747, -95.7952945),
        (23.4616577, -84.1811117),
        (25.9621554, -79.5121879),
        (33.2198245, -77.6353713),
        (41.3440684, -67.2432907)
    };

    private static double GetMinLatitude() => Boundaries.Min(b => b.lat);
    private static double GetMaxLatitude() => Boundaries.Max(b => b.lat);
    private static double GetMinLongitude() => Boundaries.Min(b => b.lng);
    private static double GetMaxLongitude() => Boundaries.Max(b => b.lng);

    private static bool IsWithinBoundaries(double lat, double lng)
    {
        double minLat = GetMinLatitude();
        double maxLat = GetMaxLatitude();
        double minLng = GetMinLongitude();
        double maxLng = GetMaxLongitude();

        return lat >= minLat && lat <= maxLat && lng >= minLng && lng <= maxLng;
    }

    private static bool IsWithinBoundaries(double lat, double lng, double deltaX, double deltaY)
    {
        double newLat = lat + deltaY * 0.0001;
        double newLng = lng + deltaX * 0.0001;
        return IsWithinBoundaries(newLat, newLng);
    }

    public static async Task Main(string[] args)
    {
        // Create Playwright and launch the browser
        var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        var page = await browser.NewPageAsync();

        // Start coordinates centered within the boundaries
        double currentLat = (GetMinLatitude() + GetMaxLatitude()) / 2;
        double currentLng = (GetMinLongitude() + GetMaxLongitude()) / 2;
        int currentZoomLevel = 5; // Initial zoom level
        const int maxZoomLevel = 21; // Maximum zoom level
        const int minZoomLevel = 3; // Minimum zoom level

        // Go to the target URL
        await page.GotoAsync($"https://www.google.com/maps/@{currentLat},{currentLng},{currentZoomLevel}z?entry=ttu");

        // Wait for the page to load completely
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Create a folder to save snapshots
        var snapshotFolder = Path.Combine(Directory.GetCurrentDirectory(), "snapshots");
        Directory.CreateDirectory(snapshotFolder);

        // Initial screenshot
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(snapshotFolder, "screenshot_0.png") });

        // Method to move the map
        async Task MoveMap(int deltaX, int deltaY)
        {
            var mapHandle = await page.QuerySelectorAsync("canvas");
            if (mapHandle == null)
            {
                Console.WriteLine("Map canvas not found.");
                return;
            }

            var boundingBox = await mapHandle.BoundingBoxAsync();
            if (boundingBox == null)
            {
                Console.WriteLine("Bounding box not found.");
                return;
            }

            await page.Mouse.MoveAsync((float)(boundingBox.X + boundingBox.Width / 2), (float)(boundingBox.Y + boundingBox.Height / 2));
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync((float)(boundingBox.X + boundingBox.Width / 2 + deltaX), (float)(boundingBox.Y + boundingBox.Height / 2 + deltaY), new MouseMoveOptions { Steps = 5 });
            await page.Mouse.UpAsync();

            // Wait for the map to stabilize
            await page.WaitForTimeoutAsync(1000);
        }

        // Method to close any unexpected pop-ups
        async Task ClosePopUps()
        {
            var popUpSelectors = new string[]
            {
                "button[aria-label='Close']", // Generic close button
                "div[aria-label='Close']",    // Another possible close element
                "button[jsaction='pane.close']", // Close button for certain pop-ups
                "div[id='consent-bump']", // Example of another common pop-up
                "button[aria-label='Dismiss']" // Another example
            };

            foreach (var selector in popUpSelectors)
            {
                try
                {
                    var popUp = await page.QuerySelectorAsync(selector);
                    if (popUp != null && await popUp.IsVisibleAsync())
                    {
                        await popUp.ClickAsync(new ElementHandleClickOptions { Timeout = 3000 }); // 3 seconds timeout for each click
                        Console.WriteLine($"Closed pop-up with selector: {selector}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while attempting to close pop-up with selector: {selector}. Exception: {ex.Message}");
                }
            }
        }

        // Method to adjust the zoom level
        async Task AdjustZoom(int levelChange)
        {
            var zoomInButton = await page.QuerySelectorAsync("button[aria-label='Zoom in']");
            var zoomOutButton = await page.QuerySelectorAsync("button[aria-label='Zoom out']");

            if (levelChange > 0 && zoomInButton != null && currentZoomLevel + levelChange <= maxZoomLevel)
            {
                for (int i = 0; i < levelChange; i++)
                {
                    await zoomInButton.ClickAsync();
                    await page.WaitForTimeoutAsync(500); // Wait for the zoom to take effect
                }
                currentZoomLevel += levelChange;
            }
            else if (levelChange < 0 && zoomOutButton != null && currentZoomLevel + levelChange >= minZoomLevel)
            {
                for (int i = 0; i < -levelChange; i++)
                {
                    await zoomOutButton.ClickAsync();
                    await page.WaitForTimeoutAsync(500); // Wait for the zoom to take effect
                }
                currentZoomLevel += levelChange;
            }
        }

        // Method to reset zoom to the original level
        async Task ResetZoom()
        {
            if (currentZoomLevel > 5)
            {
                await AdjustZoom(5 - currentZoomLevel);
            }
            else if (currentZoomLevel < 5)
            {
                await AdjustZoom(5 - currentZoomLevel);
            }
        }

        // Define the zoom stages
        var zoomStages = new[] { 3, 5, 7, 9, 12, 15, 18 };

        // Capture screenshots at each zoom stage
        int screenshotIndex = 1;
        foreach (var zoomLevel in zoomStages)
        {
            await AdjustZoom(zoomLevel - currentZoomLevel);
            var screenshotName = $"screenshot_zoom_{zoomLevel}_{screenshotIndex++}.png";
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(snapshotFolder, screenshotName) });
        }

        // Move the map and take a series of screenshots
        Random random = new Random();

        for (int i = 0; i < 250; i++)
        {
            if (i % 5 == 0) // Reset zoom and change location more frequently
            {
                await ResetZoom();
                currentLat = (GetMinLatitude() + GetMaxLatitude()) / 2 + (random.NextDouble() - 0.5) * 2; // Adjust for more randomness around the center
                currentLng = (GetMinLongitude() + GetMaxLongitude()) / 2 + (random.NextDouble() - 0.5) * 4; // Adjust for more randomness around the center
                currentZoomLevel = 5; // Reset zoom level to default
                await page.GotoAsync($"https://www.google.com/maps/@{currentLat},{currentLng},{currentZoomLevel}z?entry=ttu");
            }

            // Randomly decide to move normally or perform a larger jump
            bool largeJump = random.NextDouble() < 0.1; // 10% chance for a larger jump

            int deltaX, deltaY;
            if (largeJump)
            {
                deltaX = random.Next(-200, 200); // Larger random movement between -200 and 200 pixels
                deltaY = random.Next(-200, 200); // Larger random movement between -200 and 200 pixels
            }
            else
            {
                deltaX = random.Next(-50, 50); // Smaller random movement between -50 and 50 pixels
                deltaY = random.Next(-50, 50); // Smaller random movement between -50 and 50 pixels
            }

            // Check if the new movement is within the boundaries before making the move
            if (IsWithinBoundaries(currentLat, currentLng, deltaX, deltaY))
            {
                int zoomChange = random.Next(-1, 2); // Random zoom change between -1 and 1

                // Update current coordinates based on the movement
                double newLat = currentLat + deltaY * 0.0001; // Adjust scaling factor as needed
                double newLng = currentLng + deltaX * 0.0001; // Adjust scaling factor as needed

                // Check if the new coordinates are within the boundaries
                if (IsWithinBoundaries(newLat, newLng))
                {
                    await ClosePopUps(); // Close any unexpected pop-ups
                    await AdjustZoom(zoomChange); // Adjust zoom level
                    await MoveMap(deltaX, deltaY);

                    // Update current coordinates after moving the map
                    currentLat = newLat;
                    currentLng = newLng;

                    // Take a screenshot
                    var screenshotName = $"screenshot_{screenshotIndex++}.png";
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(snapshotFolder, screenshotName) });
                }
                else
                {
                    // Log and skip movement
                    Console.WriteLine($"Skipped movement: new coordinates ({newLat}, {newLng}) are out of bounds.");
                }
            }
            else
            {
                // Log and skip movement
                Console.WriteLine($"Skipped movement: movement out of bounds from ({currentLat}, {currentLng}) with delta ({deltaX}, {deltaY}).");
            }
        }

        // Close the browser
        await browser.CloseAsync();
    }
}
