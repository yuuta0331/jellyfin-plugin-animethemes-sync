define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function (BaseView) {
    'use strict';

    function setup(view) {
        if (view.getAttribute('data-ats-script-bound') === 'true') {
            return;
        }

        view.setAttribute('data-ats-script-bound', 'true');

var AnimeThemesSyncConfig = {
                pluginUniqueId: '66d528df-4632-4d43-9828-56957262572b'
            };

            // Helper to safely get a property value with PascalCase/camelCase fallback
            function getValue(config, key, defaultValue) {
                if (!config) return defaultValue;

                // Try PascalCase first (matches C# property name)
                var val = config[key];
                if (val !== undefined && val !== null) return val;

                // Try camelCase (Jellyfin JSON serialization default)
                var camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                val = config[camelKey];
                if (val !== undefined && val !== null) return val;

                return defaultValue;
            }

            document.querySelector('#AnimeThemesSyncConfigPage')
                .addEventListener('pageshow', function () {
                    // Add Copy Button Listener
                    var packet = this;
                    var copyBtn = packet.querySelector('#CopyCssButton');
                    var copyMsg = packet.querySelector('#CopySuccessMessage');
                    if (copyBtn) {
                        copyBtn.addEventListener('click', function () {
                            var textarea = packet.querySelector('#CustomCssText');
                            textarea.select();
                            textarea.setSelectionRange(0, 99999); // For mobile

                            try {
                                if (navigator.clipboard) {
                                    navigator.clipboard.writeText(textarea.value).then(function () {
                                        showCopySuccess();
                                    });
                                } else {
                                    document.execCommand('copy');
                                    showCopySuccess();
                                }
                            } catch (err) {
                                console.error('Copy failed', err);
                            }

                            function showCopySuccess() {
                                copyMsg.style.display = 'inline-block';
                                setTimeout(function () {
                                    copyMsg.style.display = 'none';
                                }, 3000);
                            }
                        });
                    }

                    function toggleTagOptions() {
                        var enabled = document.querySelector('#TagsEnabled').checked;
                        var container = document.querySelector('#TagOptionsContainer');
                        if (container) {
                            container.style.display = enabled ? 'block' : 'none';
                        }
                    }

                    var extrasLinkMode = document.querySelector('#ExtrasLinkMode');
                    if (extrasLinkMode) {
                        extrasLinkMode.addEventListener('change', function () {
                            document.querySelector('#ExtrasEnabled').checked = true;
                        });
                    }

                    Dashboard.showLoadingMsg();
                    ApiClient.getPluginConfiguration(AnimeThemesSyncConfig.pluginUniqueId).then(function (config) {
                        try {
                            config = config || {};

                            // Global
                            document.querySelector('#ThemeDownloadingEnabled').checked = getValue(config, 'ThemeDownloadingEnabled', true);
                            document.querySelector('#MaxConcurrentDownloads').value = getValue(config, 'MaxConcurrentDownloads', 1);
                            document.querySelector('#DownloadTimeoutSeconds').value = getValue(config, 'DownloadTimeoutSeconds', 600);
                            document.querySelector('#AllowAdd').checked = getValue(config, 'AllowAdd', true);
                            document.querySelector('#ForceRedownload').checked = getValue(config, 'ForceRedownload', false);
                            document.querySelector('#AllowDelete').checked = getValue(config, 'AllowDelete', false);
                            document.querySelector('#ExtrasEnabled').checked = getValue(config, 'ExtrasEnabled', false);
                            document.querySelector('#ExtrasLinkMode').value = normalizeExtrasLinkMode(getValue(config, 'ExtrasLinkMode', 0));
                            document.querySelector('#ExtrasFileNameFormat').value = getValue(config, 'ExtrasFileNameFormat', '{Order}. {Theme} - {Song}');
                            document.querySelector('#SeasonThemeDownloadsEnabled').checked = getValue(config, 'SeasonThemeDownloadsEnabled', true);
                            document.querySelector('#TagsEnabled').checked = getValue(config, 'TagsEnabled', true);
                            document.querySelector('#SeasonThemeMappingsJson').value = formatSeasonMappings(getValue(config, 'SeasonThemeMappings', []));

                            // Initialize Toggle
                            document.querySelector('#TagsEnabled').addEventListener('change', toggleTagOptions);
                            toggleTagOptions();

                            document.querySelector('#TagFormat').value = getValue(config, 'TagFormat', '{Season} {Year}');
                            document.querySelector('#TagSeasonSpring').value = getValue(config, 'TagSeasonSpring', 'Spring');
                            document.querySelector('#TagSeasonSummer').value = getValue(config, 'TagSeasonSummer', 'Summer');
                            document.querySelector('#TagSeasonFall').value = getValue(config, 'TagSeasonFall', 'Fall');
                            document.querySelector('#TagSeasonWinter').value = getValue(config, 'TagSeasonWinter', 'Winter');

                            // Series Audio
                            document.querySelector('#SeriesAudioMaxThemes').value = getValue(config, 'SeriesAudioMaxThemes', 1);
                            document.querySelector('#SeriesAudioVolume').value = getValue(config, 'SeriesAudioVolume', 100);
                            document.querySelector('#SeriesAudioVolumeSlider').value = getValue(config, 'SeriesAudioVolume', 100);
                            document.querySelector('#SeriesAudioIgnoreOp').checked = getValue(config, 'SeriesAudioIgnoreOp', false);
                            document.querySelector('#SeriesAudioIgnoreEd').checked = getValue(config, 'SeriesAudioIgnoreEd', true);
                            document.querySelector('#SeriesAudioIgnoreOverlaps').checked = getValue(config, 'SeriesAudioIgnoreOverlaps', false);
                            document.querySelector('#SeriesAudioIgnoreCredits').checked = getValue(config, 'SeriesAudioIgnoreCredits', false);

                            // Series Video
                            document.querySelector('#SeriesVideoMaxThemes').value = getValue(config, 'SeriesVideoMaxThemes', 1);
                            document.querySelector('#SeriesVideoVolume').value = getValue(config, 'SeriesVideoVolume', 100);
                            document.querySelector('#SeriesVideoVolumeSlider').value = getValue(config, 'SeriesVideoVolume', 100);
                            document.querySelector('#SeriesVideoIgnoreOp').checked = getValue(config, 'SeriesVideoIgnoreOp', false);
                            document.querySelector('#SeriesVideoIgnoreEd').checked = getValue(config, 'SeriesVideoIgnoreEd', true);
                            document.querySelector('#SeriesVideoIgnoreOverlaps').checked = getValue(config, 'SeriesVideoIgnoreOverlaps', false);
                            document.querySelector('#SeriesVideoIgnoreCredits').checked = getValue(config, 'SeriesVideoIgnoreCredits', false);

                            // Movie Audio
                            document.querySelector('#MovieAudioMaxThemes').value = getValue(config, 'MovieAudioMaxThemes', 1);
                            document.querySelector('#MovieAudioVolume').value = getValue(config, 'MovieAudioVolume', 100);
                            document.querySelector('#MovieAudioVolumeSlider').value = getValue(config, 'MovieAudioVolume', 100);
                            document.querySelector('#MovieAudioIgnoreOp').checked = getValue(config, 'MovieAudioIgnoreOp', false);
                            document.querySelector('#MovieAudioIgnoreEd').checked = getValue(config, 'MovieAudioIgnoreEd', true);
                            document.querySelector('#MovieAudioIgnoreOverlaps').checked = getValue(config, 'MovieAudioIgnoreOverlaps', false);
                            document.querySelector('#MovieAudioIgnoreCredits').checked = getValue(config, 'MovieAudioIgnoreCredits', false);

                            // Movie Video
                            document.querySelector('#MovieVideoMaxThemes').value = getValue(config, 'MovieVideoMaxThemes', 1);
                            document.querySelector('#MovieVideoVolume').value = getValue(config, 'MovieVideoVolume', 100);
                            document.querySelector('#MovieVideoVolumeSlider').value = getValue(config, 'MovieVideoVolume', 100);
                            document.querySelector('#MovieVideoIgnoreOp').checked = getValue(config, 'MovieVideoIgnoreOp', false);
                            document.querySelector('#MovieVideoIgnoreEd').checked = getValue(config, 'MovieVideoIgnoreEd', true);
                            document.querySelector('#MovieVideoIgnoreOverlaps').checked = getValue(config, 'MovieVideoIgnoreOverlaps', false);
                            document.querySelector('#MovieVideoIgnoreCredits').checked = getValue(config, 'MovieVideoIgnoreCredits', false);

                        } catch (e) {
                            console.error('Error populating config:', e);
                            Dashboard.alert({
                                message: 'Error loading configuration values: ' + e,
                                title: 'Error'
                            });
                        } finally {
                            Dashboard.hideLoadingMsg();
                        }
                    }).catch(function (err) {
                        Dashboard.hideLoadingMsg();
                        console.error('Failed to load AnimeThemesSync configuration: ', err);
                        Dashboard.alert({
                            message: 'Failed to load configuration: ' + err,
                            title: 'Configuration Load Error'
                        });
                    });
                });

            document.querySelector('#RunTaskButton').addEventListener('click', function () {
                Dashboard.showLoadingMsg();
                saveCurrentConfiguration(false).then(function () {
                    return ApiClient.getScheduledTasks();
                }).then(function (tasks) {
                    var task = tasks.find(function (t) {
                        return t.Key === 'AnimeThemesSyncDownloader';
                    });

                    if (task) {
                        ApiClient.startScheduledTask(task.Id).then(function () {
                            Dashboard.hideLoadingMsg();
                            Dashboard.alert('Task started.');
                        }).catch(function (err) {
                            Dashboard.hideLoadingMsg();
                            Dashboard.alert({ message: 'Failed to start task: ' + err, title: 'Task Error' });
                        });
                    } else {
                        Dashboard.hideLoadingMsg();
                        Dashboard.alert('Task not found.');
                    }
                }).catch(function (err) {
                    Dashboard.hideLoadingMsg();
                    console.error('Failed to save configuration or start task: ', err);
                    Dashboard.alert({ message: 'Failed to save configuration or start task: ' + err, title: 'Task Error' });
                });
            });

            document.querySelector('#RunOnDemandButton').addEventListener('click', function () {
                var itemId = (document.querySelector('#OnDemandItemId').value || '').trim();
                if (!itemId) {
                    Dashboard.alert({ message: 'Enter a Series, Season, or Movie Item ID.', title: 'Item ID Required' });
                    return;
                }

                var force = document.querySelector('#ForceRedownload').checked;
                Dashboard.showLoadingMsg();
                saveCurrentConfiguration(false).then(function () {
                    return ApiClient.ajax({
                        type: 'POST',
                        url: ApiClient.getUrl('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/DownloadThemes?force=' + force),
                        dataType: 'json'
                    });
                }).then(function (result) {
                    Dashboard.hideLoadingMsg();
                    result = result || {};
                    Dashboard.alert({
                        title: 'On-Demand Download Completed',
                        message: 'Downloads: ' + (result.DownloadsCompleted || 0) + '/' + (result.DownloadsPlanned || 0) +
                            ', Extras: ' + (result.ExtrasCompleted || 0) + '/' + (result.ExtrasPlanned || 0)
                    });
                }).catch(function (err) {
                    Dashboard.hideLoadingMsg();
                    console.error('Failed to run on-demand AnimeThemes download: ', err);
                    Dashboard.alert({ message: 'Failed to run on-demand download: ' + err, title: 'On-Demand Error' });
                });
            });

            document.querySelector('#AnimeThemesSyncConfigForm')
                .addEventListener('submit', function (e) {
                    e.preventDefault();
                    Dashboard.showLoadingMsg();
                    saveCurrentConfiguration(true).catch(function (err) {
                        Dashboard.hideLoadingMsg();
                        Dashboard.alert({ message: 'Failed to save configuration: ' + err, title: 'Save Error' });
                    });

                    return false;
                });

            function saveCurrentConfiguration(showResult) {
                return ApiClient.getPluginConfiguration(AnimeThemesSyncConfig.pluginUniqueId).then(function (config) {
                    applyFormValues(config);
                    return ApiClient.updatePluginConfiguration(AnimeThemesSyncConfig.pluginUniqueId, config).then(function (result) {
                        if (showResult) {
                            Dashboard.processPluginConfigurationUpdateResult(result);
                        }

                        return result;
                    });
                });
            }

            function applyFormValues(config) {
                // Global
                config.ThemeDownloadingEnabled = document.querySelector('#ThemeDownloadingEnabled').checked;
                config.MaxConcurrentDownloads = parseInt(document.querySelector('#MaxConcurrentDownloads').value) || 1;
                config.DownloadTimeoutSeconds = parseInt(document.querySelector('#DownloadTimeoutSeconds').value) || 600;
                config.AllowAdd = document.querySelector('#AllowAdd').checked;
                config.ForceRedownload = document.querySelector('#ForceRedownload').checked;
                config.AllowDelete = document.querySelector('#AllowDelete').checked;
                config.ExtrasEnabled = document.querySelector('#ExtrasEnabled').checked;
                config.ExtrasLinkMode = parseInt(document.querySelector('#ExtrasLinkMode').value) || 0;
                config.ExtrasFileNameFormat = document.querySelector('#ExtrasFileNameFormat').value;
                config.SeasonThemeDownloadsEnabled = document.querySelector('#SeasonThemeDownloadsEnabled').checked;
                config.TagsEnabled = document.querySelector('#TagsEnabled').checked;
                config.SeasonThemeMappings = parseSeasonMappings();
                config.TagFormat = document.querySelector('#TagFormat').value;
                config.TagSeasonSpring = document.querySelector('#TagSeasonSpring').value;
                config.TagSeasonSummer = document.querySelector('#TagSeasonSummer').value;
                config.TagSeasonFall = document.querySelector('#TagSeasonFall').value;
                config.TagSeasonWinter = document.querySelector('#TagSeasonWinter').value;

                // Series Audio
                config.SeriesAudioMaxThemes = parseInt(document.querySelector('#SeriesAudioMaxThemes').value) || 0;
                config.SeriesAudioVolume = parseInt(document.querySelector('#SeriesAudioVolume').value) || 0;
                config.SeriesAudioIgnoreOp = document.querySelector('#SeriesAudioIgnoreOp').checked;
                config.SeriesAudioIgnoreEd = document.querySelector('#SeriesAudioIgnoreEd').checked;
                config.SeriesAudioIgnoreOverlaps = document.querySelector('#SeriesAudioIgnoreOverlaps').checked;
                config.SeriesAudioIgnoreCredits = document.querySelector('#SeriesAudioIgnoreCredits').checked;

                // Series Video
                config.SeriesVideoMaxThemes = parseInt(document.querySelector('#SeriesVideoMaxThemes').value) || 0;
                config.SeriesVideoVolume = parseInt(document.querySelector('#SeriesVideoVolume').value) || 0;
                config.SeriesVideoIgnoreOp = document.querySelector('#SeriesVideoIgnoreOp').checked;
                config.SeriesVideoIgnoreEd = document.querySelector('#SeriesVideoIgnoreEd').checked;
                config.SeriesVideoIgnoreOverlaps = document.querySelector('#SeriesVideoIgnoreOverlaps').checked;
                config.SeriesVideoIgnoreCredits = document.querySelector('#SeriesVideoIgnoreCredits').checked;

                // Movie Audio
                config.MovieAudioMaxThemes = parseInt(document.querySelector('#MovieAudioMaxThemes').value) || 0;
                config.MovieAudioVolume = parseInt(document.querySelector('#MovieAudioVolume').value) || 0;
                config.MovieAudioIgnoreOp = document.querySelector('#MovieAudioIgnoreOp').checked;
                config.MovieAudioIgnoreEd = document.querySelector('#MovieAudioIgnoreEd').checked;
                config.MovieAudioIgnoreOverlaps = document.querySelector('#MovieAudioIgnoreOverlaps').checked;
                config.MovieAudioIgnoreCredits = document.querySelector('#MovieAudioIgnoreCredits').checked;

                // Movie Video
                config.MovieVideoMaxThemes = parseInt(document.querySelector('#MovieVideoMaxThemes').value) || 0;
                config.MovieVideoVolume = parseInt(document.querySelector('#MovieVideoVolume').value) || 0;
                config.MovieVideoIgnoreOp = document.querySelector('#MovieVideoIgnoreOp').checked;
                config.MovieVideoIgnoreEd = document.querySelector('#MovieVideoIgnoreEd').checked;
                config.MovieVideoIgnoreOverlaps = document.querySelector('#MovieVideoIgnoreOverlaps').checked;
                config.MovieVideoIgnoreCredits = document.querySelector('#MovieVideoIgnoreCredits').checked;
            }

            function normalizeExtrasLinkMode(value) {
                if (value === 'HardLinkOnly') return '1';
                if (value === 'CopyOnly') return '2';
                if (value === 'HardLinkWithCopyFallback') return '0';
                return String(value || 0);
            }

            function formatSeasonMappings(value) {
                if (!Array.isArray(value) || value.length === 0) {
                    return '[]';
                }

                return JSON.stringify(value, null, 2);
            }

            function parseSeasonMappings() {
                var textarea = document.querySelector('#SeasonThemeMappingsJson');
                var raw = textarea ? textarea.value.trim() : '';
                if (!raw) {
                    return [];
                }

                var parsed;
                try {
                    parsed = JSON.parse(raw);
                } catch (err) {
                    throw new Error('Season Mappings JSON is invalid: ' + err.message);
                }

                if (!Array.isArray(parsed)) {
                    throw new Error('Season Mappings JSON must be an array.');
                }

                return parsed;
            }

    }

    function View(view) {
        BaseView.apply(this, arguments);
        this.view = view;
        setup(view);
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function () {
        BaseView.prototype.onResume.apply(this, arguments);
        setup(this.view);
        var event = document.createEvent('Event');
        event.initEvent('pageshow', true, true);
        this.view.dispatchEvent(event);
    };

    return View;
});

