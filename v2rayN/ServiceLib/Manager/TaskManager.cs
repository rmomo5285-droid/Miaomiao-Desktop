namespace ServiceLib.Manager;

public class TaskManager
{
    private static readonly Lazy<TaskManager> _instance = new(() => new());
    public static TaskManager Instance => _instance.Value;
    private Config _config;
    private Func<bool, string, Task>? _updateFunc;

    public void RegUpdateTask(Config config, Func<bool, string, Task> updateFunc)
    {
        _config = config;
        _updateFunc = updateFunc;

        Task.Run(ScheduledTasks);
    }

    private async Task ScheduledTasks()
    {
        Logging.SaveLog("Setup Scheduled Tasks");
        await RefreshEndpointManifestAsync();

        var numOfExecuted = 1;
        while (true)
        {
            //1 minute
            await Task.Delay(1000 * 60);

            //Execute once 1 minute
            try
            {
                await UpdateTaskRunSubscription();
            }
            catch (Exception ex)
            {
                Logging.SaveLog("ScheduledTasks - UpdateTaskRunSubscription", ex);
            }

            // Refresh the small signed endpoint manifest every 6 hours after the startup check.
            // This background check never gates loading or using local profiles.
            if (numOfExecuted % 360 == 0)
            {
                await RefreshEndpointManifestAsync();
            }

            //Execute once 20 minute
            if (numOfExecuted % 20 == 0)
            {
                //Logging.SaveLog("Execute save config");

                try
                {
                    await ConfigHandler.SaveConfig(_config);
                    await ProfileExManager.Instance.SaveTo();
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - SaveConfig", ex);
                }
            }

            //Execute once 1 hour
            if (numOfExecuted % 60 == 0)
            {
                //Logging.SaveLog("Execute delete expired files");

                FileUtils.DeleteExpiredFiles(Utils.GetBinConfigPath(), DateTime.Now.AddHours(-1), "Test");
                FileUtils.DeleteExpiredFiles(Utils.GetLogPath(), DateTime.Now.AddMonths(-1));
                FileUtils.DeleteExpiredFiles(Utils.GetTempPath(), DateTime.Now.AddMonths(-1));

                try
                {
                    await UpdateTaskRunGeo(numOfExecuted / 60);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("ScheduledTasks - UpdateTaskRunGeo", ex);
                }
            }

            numOfExecuted++;
        }
    }

    private static async Task RefreshEndpointManifestAsync()
    {
        try
        {
            var manifestResult = await MiaomiaoEndpointManifestService.Instance.RefreshAsync();
            if (manifestResult.Updated || manifestResult.ShouldPrompt)
            {
                AppEvents.MiaomiaoManifestUpdated.Publish(manifestResult);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("ScheduledTasks - Miaomiao endpoint manifest", ex);
        }
    }

    private async Task UpdateTaskRunSubscription()
    {
        var updateTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
        var lstSubs = (await AppManager.Instance.SubItems())?
            .Where(t => t.AutoUpdateInterval > 0)
            .Where(t => t.Enabled && t.Url.IsNotEmpty())
            .Where(t => !MiaomiaoManagedSubscriptionPolicy.IsManaged(t)
                || MiaomiaoAccountService.Instance.IsAuthenticated)
            .Where(t => updateTime - t.UpdateTime >= t.AutoUpdateInterval * 60)
            .Where(t => t.NextAttemptTime <= updateTime)
            .ToList();

        if (lstSubs is not { Count: > 0 })
        {
            return;
        }

        Logging.SaveLog("Execute update subscription");

        foreach (var item in lstSubs)
        {
            var succeeded = false;
            await SubscriptionHandler.UpdateProcess(_config, item.Id, true, async (success, msg) =>
            {
                succeeded |= success;
                await _updateFunc?.Invoke(success, msg);
                if (success)
                {
                    Logging.SaveLog($"Update subscription end. {msg}");
                }
            });

            var latest = await AppManager.Instance.GetSubItem(item.Id) ?? item;
            if (!succeeded)
            {
                latest.ConsecutiveFailures++;
                var retryMinutes = latest.ConsecutiveFailures switch
                {
                    1 => 15,
                    2 => 60,
                    _ => 360,
                };
                latest.NextAttemptTime = updateTime + retryMinutes * 60;
            }

            await ConfigHandler.AddSubItem(_config, latest);
            await Task.Delay(1000);
        }
    }

    private async Task UpdateTaskRunGeo(int hours)
    {
        if (_config.GuiItem.AutoUpdateInterval > 0 && hours > 0 && hours % _config.GuiItem.AutoUpdateInterval == 0)
        {
            Logging.SaveLog("Execute update geo files");

            await new UpdateService(_config, async (success, msg) =>
            {
                await _updateFunc?.Invoke(false, msg);
            }).UpdateGeoFileAll();
        }
    }

}
