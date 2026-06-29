define(['loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function () {
    'use strict';

    function setup(view) {
        if (view.getAttribute('data-ats-script-bound') === 'true') {
            return;
        }

        view.setAttribute('data-ats-script-bound', 'true');

        var page = view;
        var pluginUniqueId = '66d528df-4632-4d43-9828-56957262572b';
        var state = {
            items: [],
            filteredItems: [],
            currentItem: null,
            currentResult: null,
            activeGroupId: null,
            itemsLoading: false,
            detailLoading: false,
            detailError: null,
            detailToken: 0,
            finderLoading: false,
            summaryLoading: false,
            viewMode: 'poster',
            viewSize: 'medium',
            showLibraryDetails: false,
            activeTab: 'library',
            seasonMappings: [],
            mappingExplorerRows: [],
            selectedSeason: null,
            selectedAnime: null,
            finderPreview: null,
            finderSearchTimer: null,
            finderSearchToken: 0,
            finderAutoSearched: {},
            finderTotalRecordCount: 0,
            finderLimit: 80,
            finderRequestToken: 0,
            finderCacheVersion: '',
            finderCacheReady: false,
            finderLoadError: null,
            finderObserver: null,
            finderListSearchTimer: null,
            finderPollTimer: null,
            finderScrollTop: 0,
            importConfig: null,
            mappingsImportRows: null,
            settingsConfig: null,
            settingsLoaded: false,
            settingsSnapshot: '',
            settingsApplying: false,
            pendingDownload: null,
            deletingThemeTargets: {},
            browserStartIndex: 0,
            browserLimit: 80,
            browserTotalRecordCount: 0,
            browserCacheVersion: '',
            browserCacheReady: false,
            browserRebuildRunning: false,
            browserRefreshTimer: null,
            browserRequestToken: 0,
            browserObserver: null,
            librarySearchTimer: null,
            libraryScrollTop: null,
            activeDownloads: [],
            downloadsInterval: null,
            downloadsPollInFlight: false,
            downloadsPollRequested: false,
            downloadsPollingEnabled: false,
            downloadSequence: 0,
            downloadStatuses: {},
            downloadStatusesInitialized: false,
            uiRefreshTimer: null,
            uiRefreshInFlight: false,
            uiRefreshQueued: false,
            refreshFinderRequested: false,
            refreshMappingsRequested: false,
            playerLoadToken: 0,
            playerLoadTimer: null,
            themeObserver: null,
            themeMediaQuery: null,
            themeMediaHandler: null
        };
        var browserToolbar = page.querySelector('.ats-browser-toolbar');
        var itemSelect = page.querySelector('#AnimeThemesBrowserItemSelect');
        var libraryView = page.querySelector('#AnimeThemesBrowserLibraryView');
        var detailView = page.querySelector('#AnimeThemesBrowserDetailView');
        var manageView = page.querySelector('#AnimeThemesBrowserManageView');
        var finderView = page.querySelector('#AnimeThemesSeasonFinderView');
        var settingsView = page.querySelector('#AnimeThemesBrowserSettingsView');
        var itemGrid = page.querySelector('#AnimeThemesBrowserItemGrid');
        var itemPager = page.querySelector('#AnimeThemesBrowserPager');
        var libraryCount = page.querySelector('#AnimeThemesBrowserLibraryCount');
        var rowsContainer = page.querySelector('#AnimeThemesBrowserRows');
        var seasonGroups = page.querySelector('#AnimeThemesBrowserSeasonGroups');
        var searchInput = page.querySelector('#AnimeThemesBrowserSearch');
        var libraryTypeFilter = page.querySelector('#AnimeThemesBrowserLibraryTypeFilter');
        var libraryLinkFilter = page.querySelector('#AnimeThemesBrowserLibraryLinkFilter');
        var librarySavedFilter = page.querySelector('#AnimeThemesBrowserLibrarySavedFilter');
        var librarySort = page.querySelector('#AnimeThemesBrowserLibrarySort');
        var librarySortDirection = page.querySelector('#AnimeThemesBrowserLibrarySortDirection');
        var showDetailsCheckbox = page.querySelector('#AnimeThemesBrowserShowDetails');
        var themeSearchInput = page.querySelector('#AnimeThemesBrowserThemeSearch');
        var filtersPanel = page.querySelector('.ats-filters');
        var gridSizeSelect = page.querySelector('#AnimeThemesBrowserGridSize');
        var typeFilter = page.querySelector('#AnimeThemesBrowserTypeFilter');
        var statusFilter = page.querySelector('#AnimeThemesBrowserStatusFilter');
        var flagFilter = page.querySelector('#AnimeThemesBrowserFlagFilter');
        var title = page.querySelector('#AnimeThemesBrowserTitle');
        var meta = page.querySelector('#AnimeThemesBrowserMeta');
        var poster = page.querySelector('#AnimeThemesBrowserPoster');
        var posterFallback = page.querySelector('#AnimeThemesBrowserPosterFallback');
        var logo = page.querySelector('#AnimeThemesBrowserLogo');
        var backdrop = page.querySelector('#AnimeThemesBrowserBackdrop');
        var player = page.querySelector('#AnimeThemesBrowserPlayer');
        var playerBody = page.querySelector('#AnimeThemesBrowserPlayerBody');
        var playerTitle = page.querySelector('#AnimeThemesBrowserPlayerTitle');

        var downloadDialog = page.querySelector('#AnimeThemesDownloadDialog');
        var downloadDialogTheme = page.querySelector('#AnimeThemesDownloadDialogTheme');
        var downloadDialogError = page.querySelector('#AnimeThemesDownloadDialogError');
        var downloadIncludeVideo = page.querySelector('#AtsDownloadIncludeVideo');
        var downloadIncludeAudio = page.querySelector('#AtsDownloadIncludeAudio');
        var downloadIncludeExtras = page.querySelector('#AtsDownloadIncludeExtras');
        var downloadDialogConfirm = page.querySelector('#AnimeThemesDownloadDialogConfirm');

        var deleteDialog = page.querySelector('#AnimeThemesDeleteDialog');
        var deleteDialogTheme = page.querySelector('#AnimeThemesDeleteDialogTheme');
        var deleteDialogError = page.querySelector('#AnimeThemesDeleteDialogError');
        var deleteIncludeVideo = page.querySelector('#AtsDeleteIncludeVideo');
        var deleteIncludeAudio = page.querySelector('#AtsDeleteIncludeAudio');
        var deleteIncludeExtras = page.querySelector('#AtsDeleteIncludeExtras');
        var deleteDialogConfirm = page.querySelector('#AnimeThemesDeleteDialogConfirm');

        var downloadManager = page.querySelector('#AnimeThemesDownloadManager');
        var dmBadge = page.querySelector('#AnimeThemesDmBadge');
        var dmToggle = page.querySelector('#AnimeThemesDmToggle');
        var dmContent = page.querySelector('#AnimeThemesDmContent');
        var dmClearHistory = page.querySelector('#AnimeThemesDmClearHistory');
        var progressPanel = page.querySelector('#AnimeThemesBrowserProgress');
        var progressText = page.querySelector('#AnimeThemesBrowserProgressText');
        var progressPercent = page.querySelector('#AnimeThemesBrowserProgressPercent');
        var progressBar = page.querySelector('#AnimeThemesBrowserProgressBar');
        var summaryItems = page.querySelector('#AnimeThemesSummaryItems');
        var summarySeriesItems = page.querySelector('#AnimeThemesSummarySeriesItems');
        var summaryMovieItems = page.querySelector('#AnimeThemesSummaryMovieItems');
        var summarySeasonItems = page.querySelector('#AnimeThemesSummarySeasonItems');
        var summarySavedItems = page.querySelector('#AnimeThemesSummarySavedItems');
        var summaryVideos = page.querySelector('#AnimeThemesSummaryVideos');
        var summarySongs = page.querySelector('#AnimeThemesSummarySongs');
        var summaryExtras = page.querySelector('#AnimeThemesSummaryExtras');
        var summaryBytes = page.querySelector('#AnimeThemesSummaryBytes');
        var cacheBytes = page.querySelector('#AnimeThemesCacheBytes');
        var cacheItems = page.querySelector('#AnimeThemesCacheItems');
        var cacheState = page.querySelector('#AnimeThemesCacheState');
        var cachePath = page.querySelector('#AnimeThemesCachePath');
        var summaryManualSeasonMappings = page.querySelector('#AnimeThemesSummaryManualSeasonMappings');
        var summaryAutoSeasonMappings = page.querySelector('#AnimeThemesSummaryAutoSeasonMappings');
        var summaryDirectSeasonMappings = page.querySelector('#AnimeThemesSummaryDirectSeasonMappings');
        var summarySeriesSharedSeasons = page.querySelector('#AnimeThemesSummarySeriesSharedSeasons');
        var summaryUnmatchedSeasons = page.querySelector('#AnimeThemesSummaryUnmatchedSeasons');
        var seasonFilter = page.querySelector('#AnimeThemesSeasonFilter');
        var seasonList = page.querySelector('#AnimeThemesSeasonList');
        var seasonSearch = page.querySelector('#AnimeThemesSeasonSearch');
        var seasonSort = page.querySelector('#AnimeThemesSeasonSort');
        var seasonSortDirection = page.querySelector('#AnimeThemesSeasonSortDirection');
        var finderSearchInput = page.querySelector('#AnimeThemesFinderSearchInput');
        var finderYear = page.querySelector('#AnimeThemesFinderYear');
        var finderResults = page.querySelector('#AnimeThemesFinderResults');
        var finderPreview = page.querySelector('#AnimeThemesFinderPreview');
        var finderState = page.querySelector('#AnimeThemesFinderState');
        var downloadItemButton = page.querySelector('#AnimeThemesBrowserDownload');
        var matchInFinderButton = page.querySelector('#AnimeThemesBrowserMatchInFinder');
        var settingsState = page.querySelector('#AnimeThemesSettingsState');
        var importFileInput = page.querySelector('#AtsImportFile');
        var importJsonInput = page.querySelector('#AtsImportJson');
        var importPreview = page.querySelector('#AtsImportPreview');
        var importApplyButton = page.querySelector('#AtsImportApply');
        var importState = page.querySelector('#AtsImportState');
        var mappingsFileInput = page.querySelector('#AtsMappingsFile');
        var mappingsJsonInput = page.querySelector('#AtsMappingsJson');
        var mappingsPreview = page.querySelector('#AtsMappingsPreview');
        var mappingsApplyButton = page.querySelector('#AtsMappingsApply');
        var mappingsState = page.querySelector('#AtsMappingsState');
        var explorerSearch = page.querySelector('#AtsExplorerSearch');
        var explorerStatus = page.querySelector('#AtsExplorerStatus');
        var explorerTable = page.querySelector('#AtsExplorerTable');
        var settingsFields = {
            ThemeDownloadingEnabled: page.querySelector('#AtsThemeDownloadingEnabled'),
            MaxConcurrentDownloads: page.querySelector('#AtsMaxConcurrentDownloads'),
            DownloadTimeoutSeconds: page.querySelector('#AtsDownloadTimeoutSeconds'),
            SegmentedDownloadEnabled: page.querySelector('#AtsSegmentedDownloadEnabled'),
            SegmentedDownloadSegments: page.querySelector('#AtsSegmentedDownloadSegments'),
            SegmentedDownloadOptions: page.querySelector('#AtsSegmentedDownloadOptions'),
            AllowAdd: page.querySelector('#AtsAllowAdd'),
            ForceRedownload: page.querySelector('#AtsForceRedownload'),
            AllowDelete: page.querySelector('#AtsAllowDelete'),
            SeasonThemeDownloadsEnabled: page.querySelector('#AtsSeasonThemeDownloadsEnabled'),
            ExtrasEnabled: page.querySelector('#AtsExtrasEnabled'),
            ExtrasOptions: page.querySelector('#AtsExtrasOptions'),
            ExtrasLinkMode: page.querySelector('#AtsExtrasLinkMode'),
            ExtrasFileSuffix: page.querySelector('#AtsExtrasFileSuffix'),
            ExtrasFileNameFormat: page.querySelector('#AtsExtrasFileNameFormat'),
            ExtrasFormatPreview: page.querySelector('#AtsExtrasFormatPreview'),
            TagsEnabled: page.querySelector('#AtsTagsEnabled'),
            TagOptions: page.querySelector('#AtsTagOptions'),
            TagFormat: page.querySelector('#AtsTagFormat'),
            TagFormatPreview: page.querySelector('#AtsTagFormatPreview'),
            TagSeasonSpring: page.querySelector('#AtsTagSeasonSpring'),
            TagSeasonSummer: page.querySelector('#AtsTagSeasonSummer'),
            TagSeasonFall: page.querySelector('#AtsTagSeasonFall'),
            TagSeasonWinter: page.querySelector('#AtsTagSeasonWinter'),
            CustomCssText: page.querySelector('#AtsCustomCssText'),
            CopyCssButton: page.querySelector('#AtsCopyCssButton'),
            CopyCssMessage: page.querySelector('#AtsCopyCssMessage'),
            SaveButton: page.querySelector('#AtsSettingsSave'),
            ResetDefaultsButton: page.querySelector('#AtsResetDefaults')
        };

        function value(obj, pascal, camel) {
            return obj ? obj[pascal] !== undefined ? obj[pascal] : obj[camel] : null;
        }

        function text(raw) {
            return raw === null || raw === undefined || raw === '' ? '-' : String(raw);
        }

        function apiRequest(options) {
            return Promise.resolve(ApiClient.ajax(options)).catch(function (err) {
                return normalizeApiError(err).then(function (normalized) {
                    throw normalized;
                });
            });
        }

        function errorMessageFromPayload(payload) {
            if (!payload) return '';
            if (typeof payload === 'string') {
                var trimmed = payload.trim();
                if (!trimmed) return '';
                try {
                    return errorMessageFromPayload(JSON.parse(trimmed)) || trimmed;
                } catch (ignored) {
                    return trimmed;
                }
            }

            return payload.error || payload.Error || payload.message || payload.Message ||
                (payload.ResponseStatus && (payload.ResponseStatus.Message || payload.ResponseStatus.ErrorCode)) ||
                (payload.responseStatus && (payload.responseStatus.message || payload.responseStatus.errorCode)) || '';
        }

        function normalizeApiError(err) {
            var immediate = errorMessageFromPayload(err && err.responseJSON) ||
                errorMessageFromPayload(err && err.responseText) ||
                (err && err.message && err.message !== '[object Response]' ? err.message : '');
            if (immediate) return Promise.resolve(new Error(immediate));

            var response = err && typeof err.clone === 'function' ? err.clone() : err;
            if (response && typeof response.text === 'function') {
                return response.text().then(function (body) {
                    var message = errorMessageFromPayload(body) || response.statusText || ('HTTP ' + response.status);
                    return new Error(message);
                }).catch(function () {
                    return new Error((response && response.statusText) || 'Request failed.');
                });
            }

            return Promise.resolve(err instanceof Error ? err : new Error(String(err || 'Unknown error')));
        }

        function apiGet(path) {
            return apiRequest({ type: 'GET', url: ApiClient.getUrl(path), dataType: 'json' });
        }

        function apiPost(path) {
            return apiRequest({ type: 'POST', url: ApiClient.getUrl(path), dataType: 'json' });
        }

        function apiPostJson(path, data) {
            return apiRequest({
                type: 'POST',
                url: ApiClient.getUrl(path),
                dataType: 'json',
                contentType: 'application/json',
                data: JSON.stringify(data || {})
            });
        }

        function apiDelete(path) {
            return apiRequest({ type: 'DELETE', url: ApiClient.getUrl(path), dataType: 'json' });
        }

        function apiDeleteNoContent(path) {
            return apiRequest({ type: 'DELETE', url: ApiClient.getUrl(path) });
        }

        function apiUrl(path, authenticated) {
            if (!path) {
                return '';
            }

            if (/^(https?:)?\/\//i.test(path) || /^(data|blob):/i.test(path)) {
                return path;
            }

            var url = ApiClient.getUrl(path);
            if (!authenticated) {
                return url;
            }

            var token = getAccessToken();
            return token ? appendQuery(url, 'api_key', token) : url;
        }

        function getAccessToken() {
            try {
                if (typeof ApiClient.accessToken === 'function') {
                    return ApiClient.accessToken.call(ApiClient);
                }

                if (typeof ApiClient.accessToken === 'string') {
                    return ApiClient.accessToken;
                }

                var serverInfo = typeof ApiClient.serverInfo === 'function'
                    ? ApiClient.serverInfo.call(ApiClient)
                    : ApiClient.serverInfo || ApiClient._serverInfo;
                return serverInfo && (serverInfo.AccessToken || serverInfo.accessToken);
            } catch (err) {
                return null;
            }
        }

        function appendQuery(url, key, rawValue) {
            var separator = url.indexOf('?') === -1 ? '?' : '&';
            return url + separator + encodeURIComponent(key) + '=' + encodeURIComponent(rawValue);
        }

        function formatBytes(value) {
            var bytes = Number(value || 0);
            if (bytes < 1024) return bytes + ' B';
            var units = ['KB', 'MB', 'GB', 'TB'];
            var size = bytes / 1024;
            var index = 0;
            while (size >= 1024 && index < units.length - 1) {
                size /= 1024;
                index++;
            }
            return size.toFixed(size >= 10 ? 1 : 2) + ' ' + units[index];
        }

        function setProgress(visible, message, progress) {
            progressPanel.style.display = visible ? '' : 'none';
            var value = Math.max(0, Math.min(100, Math.round(progress || 0)));
            progressText.textContent = message || '';
            progressPercent.textContent = value + '%';
            progressBar.style.width = value + '%';
        }

        function getErrorMessage(err) {
            if (!err) return 'Unknown error';
            if (err.message) return err.message;
            if (err.responseJSON && err.responseJSON.error) return err.responseJSON.error;
            if (err.responseText) return err.responseText;
            return String(err);
        }

        function setImage(img, path, fallback) {
            img.classList.remove('ats-fade-in');
            img.style.display = 'none';
            img.removeAttribute('src');
            if (fallback) fallback.style.display = '';
            if (!path) {
                img.classList.remove('ats-loading-blur');
                return;
            }
            img.onload = function () {
                img.style.display = 'block';
                img.classList.add('ats-fade-in');
                img.classList.remove('ats-loading-blur');
                if (fallback) fallback.style.display = 'none';
            };
            img.onerror = function () {
                img.style.display = 'none';
                img.classList.remove('ats-loading-blur');
                if (fallback) fallback.style.display = '';
            };
            img.src = apiUrl(path, false);
        }

        function setBackdrop(path) {
            backdrop.style.backgroundImage = path ? 'url("' + apiUrl(path, false) + '")' : '';
            var bg = page.querySelector('.ats-hero-bg') || backdrop;
            if (bg) {
                bg.classList.remove('ats-loading-blur');
            }
        }

        function selectedItem(id) {
            var targetId = id || itemSelect.value;
            if (!targetId) return null;
            if (state.currentItem && String(value(state.currentItem, 'Id', 'id')) === String(targetId)) {
                return state.currentItem;
            }
            return state.items.find(function (item) { return String(value(item, 'Id', 'id')) === String(targetId); }) || null;
        }

        function groupId(group) {
            return String(value(group, 'SeasonItemId', 'seasonItemId') || value(group, 'ItemId', 'itemId') || '') + ':' + String(value(group, 'Type', 'type') || '');
        }

        function getGroups() {
            var groups = (value(state.currentResult, 'Groups', 'groups') || []).filter(function (group) {
                return !isSpecialGroup(group);
            });
            return groups.sort(function (left, right) {
                var leftSpecial = isSpecialGroup(left);
                var rightSpecial = isSpecialGroup(right);
                if (leftSpecial !== rightSpecial) return leftSpecial ? 1 : -1;
                var leftNumber = value(left, 'SeasonNumber', 'seasonNumber');
                var rightNumber = value(right, 'SeasonNumber', 'seasonNumber');
                if (leftNumber === null || leftNumber === undefined) leftNumber = 9999;
                if (rightNumber === null || rightNumber === undefined) rightNumber = 9999;
                return leftNumber - rightNumber;
            });
        }

        function isSpecialGroup(group) {
            var seasonNumber = value(group, 'SeasonNumber', 'seasonNumber');
            var name = String(value(group, 'Name', 'name') || '').toLowerCase();
            return seasonNumber === 0 || name.indexOf('special') !== -1;
        }

        function seasonMappingHasMatch(row) {
            var status = String(value(row, 'Status', 'status') || '').toLowerCase();
            return status === 'manual' ||
                status === 'auto' ||
                status === 'direct' ||
                status === 'series' ||
                !!value(row, 'AnimeThemesSlug', 'animeThemesSlug') ||
                !!value(row, 'AnimeName', 'animeName') ||
                !!value(row, 'AnimeThemesId', 'animeThemesId') ||
                !!value(row, 'AniListId', 'aniListId') ||
                !!value(row, 'MyAnimeListId', 'myAnimeListId');
        }

        function selectDefaultGroup(groups) {
            if (!groups.length) return null;
            var seasonOne = groups.find(function (group) {
                return value(group, 'Type', 'type') === 'Season' && value(group, 'SeasonNumber', 'seasonNumber') === 1;
            });
            if (seasonOne) return groupId(seasonOne);
            var normalWithThemes = groups.find(function (group) {
                return value(group, 'Type', 'type') === 'Season' &&
                    !isSpecialGroup(group) &&
                    (value(group, 'Themes', 'themes') || []).length > 0;
            });
            if (normalWithThemes) return groupId(normalWithThemes);
            var normalSeason = groups.find(function (group) {
                return value(group, 'Type', 'type') === 'Season' && !isSpecialGroup(group);
            });
            return groupId(normalSeason || groups[0]);
        }

        function activeGroup() {
            var groups = getGroups();
            if (groups.length) {
                return groups.find(function (group) { return groupId(group) === state.activeGroupId; }) || groups[0];
            }

            return state.currentResult || null;
        }

        function activeGroupItemId() {
            var group = activeGroup();
            return value(group, 'ItemId', 'itemId') || itemSelect.value;
        }

        function activeRows() {
            var group = activeGroup();
            return value(group, 'Themes', 'themes') || value(state.currentResult, 'Themes', 'themes') || [];
        }

        function savedCount(item) {
            return Number(value(item, 'ThemeVideos', 'themeVideos') || 0) +
                Number(value(item, 'ThemeSongs', 'themeSongs') || 0) +
                Number(value(item, 'Extras', 'extras') || 0);
        }

        function itemHasAnyExternalId(item) {
            return !!(value(item, 'AnimeThemesSlug', 'animeThemesSlug') ||
                value(item, 'AniListId', 'aniListId') ||
                value(item, 'MyAnimeListId', 'myAnimeListId'));
        }

        function itemLinkStatus(item) {
            var status = value(item, 'LinkStatus', 'linkStatus');
            if (status) return String(status);
            if (value(item, 'HasDirectLink', 'hasDirectLink') || value(item, 'AnimeThemesSlug', 'animeThemesSlug')) return 'Direct';
            if (value(item, 'HasManualSeasonLink', 'hasManualSeasonLink')) return 'Manual';
            return 'Unlinked';
        }

        function itemIsLinked(item) {
            return itemLinkStatus(item).toLowerCase() !== 'unlinked';
        }

        function itemDateValue(item, pascal, camel) {
            var raw = value(item, pascal, camel);
            if (!raw) return null;
            var date = new Date(raw);
            return isNaN(date.getTime()) ? null : date.getTime();
        }

        function compareNullableDates(left, right, direction) {
            var leftMissing = left === null || left === undefined;
            var rightMissing = right === null || right === undefined;
            if (leftMissing && rightMissing) return 0;
            if (leftMissing) return 1;
            if (rightMissing) return -1;
            return (left - right) * direction;
        }

        function itemMatchesSearch(item) {
            var query = searchInput.value.trim().toLowerCase();
            if (query) {
                var haystack = [
                    value(item, 'Name', 'name'),
                    value(item, 'Type', 'type'),
                    value(item, 'AnimeThemesSlug', 'animeThemesSlug'),
                    value(item, 'AniListId', 'aniListId'),
                    value(item, 'MyAnimeListId', 'myAnimeListId')
                ].join(' ').toLowerCase();
                if (haystack.indexOf(query) === -1) return false;
            }

            var type = libraryTypeFilter ? libraryTypeFilter.value : 'all';
            if (type !== 'all' && String(value(item, 'Type', 'type') || '').toLowerCase() !== type) return false;

            var link = libraryLinkFilter ? libraryLinkFilter.value : 'all';
            if (link === 'linked' && !itemIsLinked(item)) return false;
            if (link === 'unlinked' && itemIsLinked(item)) return false;
            if (link === 'external' && !itemHasAnyExternalId(item)) return false;

            var saved = savedCount(item);
            var savedFilter = librarySavedFilter ? librarySavedFilter.value : 'all';
            if (savedFilter === 'saved' && saved <= 0) return false;
            if (savedFilter === 'missing' && saved > 0) return false;
            if (savedFilter === 'video' && Number(value(item, 'ThemeVideos', 'themeVideos') || 0) <= 0) return false;
            if (savedFilter === 'audio' && Number(value(item, 'ThemeSongs', 'themeSongs') || 0) <= 0) return false;
            if (savedFilter === 'extras' && Number(value(item, 'Extras', 'extras') || 0) <= 0) return false;
            return true;
        }

        function compareItems(left, right) {
            var sort = librarySort ? librarySort.value : 'name';
            var direction = librarySortDirection && librarySortDirection.value === 'desc' ? -1 : 1;
            var result = 0;
            if (sort === 'type') {
                result = String(value(left, 'Type', 'type') || '').localeCompare(String(value(right, 'Type', 'type') || ''), undefined, { sensitivity: 'base' });
            } else if (sort === 'saved') {
                result = savedCount(left) - savedCount(right);
            } else if (sort === 'size') {
                result = Number(value(left, 'TotalBytes', 'totalBytes') || 0) - Number(value(right, 'TotalBytes', 'totalBytes') || 0);
            } else if (sort === 'link') {
                result = itemLinkStatus(left).localeCompare(itemLinkStatus(right), undefined, { sensitivity: 'base' });
            } else if (sort === 'itemAdded') {
                result = compareNullableDates(itemDateValue(left, 'DateCreated', 'dateCreated'), itemDateValue(right, 'DateCreated', 'dateCreated'), direction);
                if (result !== 0) return result;
            } else if (sort === 'latestEpisodeAdded') {
                result = compareNullableDates(itemDateValue(left, 'LatestEpisodeDateCreated', 'latestEpisodeDateCreated'), itemDateValue(right, 'LatestEpisodeDateCreated', 'latestEpisodeDateCreated'), direction);
                if (result !== 0) return result;
            }

            if (result === 0) {
                result = String(value(left, 'Name', 'name') || '').localeCompare(String(value(right, 'Name', 'name') || ''), undefined, { sensitivity: 'base' });
            }

            return result * direction;
        }

        function renderItemOptions(append) {
            var previous = itemSelect.value;
            state.filteredItems = state.items.slice();
            itemSelect.innerHTML = '';
            state.filteredItems.forEach(function (item) {
                var option = document.createElement('option');
                option.value = value(item, 'Id', 'id');
                option.textContent = text(value(item, 'Name', 'name')) + ' (' + text(value(item, 'Type', 'type')) + ')';
                itemSelect.appendChild(option);
            });
            if (previous && state.filteredItems.some(function (item) { return String(value(item, 'Id', 'id')) === previous; })) {
                itemSelect.value = previous;
            }
            renderItemGrid(append);
        }

        function createSkeleton(className) {
            var div = document.createElement('div');
            div.className = 'ats-skeleton' + (className ? ' ' + className : '');
            return div;
        }

        function appendEmptyState(container, titleText, detailText) {
            var empty = document.createElement('div');
            empty.className = 'ats-empty-state ats-fade-in';
            appendDiv(empty, 'ats-empty-title', titleText);
            if (detailText) {
                appendDiv(empty, 'ats-empty-detail', detailText);
            }
            container.appendChild(empty);
            return empty;
        }

        function renderLibrarySkeleton() {
            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize + ' ' + (state.showLibraryDetails ? 'show-details' : 'hide-details');
            itemGrid.innerHTML = '';
            if (itemPager) itemPager.innerHTML = '';
            libraryCount.textContent = 'Loading library...';
            for (var i = 0; i < 12; i++) {
                var card = document.createElement('div');
                card.className = 'ats-item-card ats-placeholder-card';
                card.appendChild(createSkeleton('ats-skeleton-media'));
                if (state.viewMode === 'list') {
                    var body = document.createElement('div');
                    body.appendChild(createSkeleton('ats-skeleton-line wide'));
                    body.appendChild(createSkeleton('ats-skeleton-line'));
                    card.appendChild(body);
                }
                itemGrid.appendChild(card);
            }
        }

        function renderSummarySkeleton() {
            [
                summaryItems, summarySeriesItems, summaryMovieItems, summarySeasonItems, summarySavedItems,
                summaryVideos, summarySongs, summaryExtras, summaryBytes, cacheBytes, cacheItems, cacheState,
                summaryManualSeasonMappings, summaryAutoSeasonMappings, summaryDirectSeasonMappings,
                summarySeriesSharedSeasons, summaryUnmatchedSeasons
            ].forEach(function (node) {
                if (node) node.innerHTML = '<span class="ats-skeleton ats-skeleton-text"></span>';
            });
        }

        function renderFinderSkeleton(target) {
            var container = target || seasonList;
            container.innerHTML = '';
            for (var i = 0; i < 5; i++) {
                var card = document.createElement('div');
                card.className = 'ats-season-card ats-placeholder-card';
                card.appendChild(createSkeleton('ats-skeleton-line wide'));
                card.appendChild(createSkeleton('ats-skeleton-line'));
                card.appendChild(createSkeleton('ats-skeleton-line short'));
                container.appendChild(card);
            }
        }

        function renderItemGrid(append) {
            if (state.itemsLoading && !append) {
                renderLibrarySkeleton();
                return;
            }

            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize + ' ' + (state.showLibraryDetails ? 'show-details' : 'hide-details');
            if (itemPager) itemPager.innerHTML = '';

            if (!append) {
                itemGrid.innerHTML = '';
            } else {
                var moreBtn = itemGrid.querySelector('.ats-load-more-indicator');
                if (moreBtn) moreBtn.remove();
            }

            libraryCount.textContent = state.items.length + ' / ' + state.browserTotalRecordCount + ' items' + (state.browserRebuildRunning ? ' | updating' : '');
            if (!state.filteredItems.length) {
                appendEmptyState(
                    itemGrid,
                    state.browserCacheReady ? 'No library items found' : 'Updating library...',
                    state.browserCacheReady ? 'Try a different search or refresh the library.' : 'The library view will update automatically.');
                return;
            }

            var itemsToRender = append ? state.filteredItems.slice(state.items.length - state.browserLimit) : state.filteredItems;
            itemsToRender.forEach(function (item) {
                itemGrid.appendChild(createItemCard(item));
            });

            if (state.items.length < state.browserTotalRecordCount) {
                var loader = document.createElement('div');
                loader.className = 'ats-load-more-indicator';
                loader.style.height = '1px';
                itemGrid.appendChild(loader);
                observeBrowserSentinel(loader);
            } else if (state.browserObserver) {
                state.browserObserver.disconnect();
                state.browserObserver = null;
            }

            attachScrollListener();
        }

        function createItemCard(item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-item-card ats-fade-in';
            var name = value(item, 'Name', 'name') || 'Unknown';
            button.setAttribute('data-item-id', value(item, 'Id', 'id'));
            button.setAttribute('aria-label', name);
            button.title = name;

            var imageWrap = document.createElement('div');
            imageWrap.className = 'ats-item-image';
            var image = document.createElement('img');
            image.alt = '';
            var fallback = document.createElement('div');
            fallback.className = 'ats-item-fallback';
            fallback.textContent = String(name).trim().slice(0, 2).toUpperCase();
            imageWrap.appendChild(image);
            imageWrap.appendChild(fallback);
            button.appendChild(imageWrap);

            var textWrap = document.createElement('div');
            textWrap.className = 'ats-item-text';
            appendDiv(textWrap, 'ats-item-title', name);
            appendDiv(textWrap, 'ats-item-meta', [
                value(item, 'Type', 'type'),
                value(item, 'AnimeThemesSlug', 'animeThemesSlug') || value(item, 'AniListId', 'aniListId') || value(item, 'MyAnimeListId', 'myAnimeListId') || itemLinkStatus(item),
                savedCount(item) ? savedCount(item) + ' saved' : 'No saved files'
            ].filter(Boolean).join(' | '));
            var status = document.createElement('div');
            status.className = 'ats-item-status';
            addChip(status, 'Video ' + Number(value(item, 'ThemeVideos', 'themeVideos') || 0), Number(value(item, 'ThemeVideos', 'themeVideos') || 0) > 0 ? 'ok' : 'missing');
            addChip(status, 'Audio ' + Number(value(item, 'ThemeSongs', 'themeSongs') || 0), Number(value(item, 'ThemeSongs', 'themeSongs') || 0) > 0 ? 'ok' : 'missing');
            addChip(status, 'Extras ' + Number(value(item, 'Extras', 'extras') || 0), Number(value(item, 'Extras', 'extras') || 0) > 0 ? 'ok' : 'missing');
            textWrap.appendChild(status);
            button.appendChild(textWrap);

            var imagePath = state.viewMode === 'poster'
                ? value(item, 'PrimaryImageUrl', 'primaryImageUrl')
                : value(item, 'ThumbImageUrl', 'thumbImageUrl') || value(item, 'BackdropImageUrl', 'backdropImageUrl') || value(item, 'PrimaryImageUrl', 'primaryImageUrl');
            setImage(image, imagePath, fallback);
            button.addEventListener('click', function () {
                openItemDetail(value(item, 'Id', 'id'));
            });
            return button;
        }

        function setViewMode(mode) {
            state.viewMode = mode === 'list' || mode === 'thumb' ? mode : 'poster';
            page.querySelectorAll('.ats-view-mode').forEach(function (button) {
                button.classList.toggle('active', button.getAttribute('data-view-mode') === state.viewMode);
            });
            renderItemGrid();
        }

        function setViewSize(size) {
            state.viewSize = size === 'compact' || size === 'large' ? size : 'medium';
            if (gridSizeSelect.value !== state.viewSize) {
                gridSizeSelect.value = state.viewSize;
            }
            renderItemGrid();
        }

        function setActiveTab(tab) {
            state.activeTab = tab === 'manage' || tab === 'finder' || tab === 'settings' ? tab : 'library';
            syncLayout();
        }

        function parsedColor(valueToParse) {
            var match = String(valueToParse || '').match(/rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:\s*[,/]\s*([\d.]+))?\s*\)/i);
            if (!match) return null;
            return {
                r: Number(match[1]),
                g: Number(match[2]),
                b: Number(match[3]),
                a: match[4] === undefined ? 1 : Number(match[4])
            };
        }

        function colorLuminance(color) {
            function channel(valueToConvert) {
                var normalized = valueToConvert / 255;
                return normalized <= 0.03928 ? normalized / 12.92 : Math.pow((normalized + 0.055) / 1.055, 2.4);
            }
            return (0.2126 * channel(color.r)) + (0.7152 * channel(color.g)) + (0.0722 * channel(color.b));
        }

        function detectDarkTheme() {
            var node = page;
            while (node && node.nodeType === 1) {
                var background = parsedColor(window.getComputedStyle(node).backgroundColor);
                if (background && background.a > 0.05) return colorLuminance(background) < 0.45;
                node = node.parentElement;
            }

            var foreground = parsedColor(window.getComputedStyle(page).color) || parsedColor(window.getComputedStyle(document.body).color);
            if (foreground) return colorLuminance(foreground) > 0.55;
            return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
        }

        function syncThemeMode() {
            page.setAttribute('data-ats-theme', detectDarkTheme() ? 'dark' : 'light');
        }

        function setupThemeObserver() {
            teardownThemeObserver();
            syncThemeMode();
            state.themeObserver = new MutationObserver(syncThemeMode);
            state.themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme'] });
            if (document.body) state.themeObserver.observe(document.body, { attributes: true, attributeFilter: ['class', 'style', 'data-theme'] });

            if (window.matchMedia) {
                state.themeMediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
                state.themeMediaHandler = syncThemeMode;
                if (state.themeMediaQuery.addEventListener) state.themeMediaQuery.addEventListener('change', state.themeMediaHandler);
                else if (state.themeMediaQuery.addListener) state.themeMediaQuery.addListener(state.themeMediaHandler);
            }
        }

        function teardownThemeObserver() {
            if (state.themeObserver) state.themeObserver.disconnect();
            state.themeObserver = null;
            if (state.themeMediaQuery && state.themeMediaHandler) {
                if (state.themeMediaQuery.removeEventListener) state.themeMediaQuery.removeEventListener('change', state.themeMediaHandler);
                else if (state.themeMediaQuery.removeListener) state.themeMediaQuery.removeListener(state.themeMediaHandler);
            }
            state.themeMediaQuery = null;
            state.themeMediaHandler = null;
        }

        function showDetailLoading() {
            detailView.setAttribute('aria-busy', 'true');
        }

        function hideDetailLoading(token) {
            if (token && token !== state.detailToken) return;
            detailView.setAttribute('aria-busy', 'false');
        }

        function syncLayout() {
            page.querySelectorAll('.ats-tab').forEach(function (button) {
                button.classList.toggle('active', button.getAttribute('data-ats-tab') === state.activeTab);
            });
            var libraryActive = state.activeTab === 'library';
            var finderActive = state.activeTab === 'finder';
            var manageActive = state.activeTab === 'manage';
            var settingsActive = state.activeTab === 'settings';
            var detailActive = libraryActive && (state.detailLoading || state.detailError || !!state.currentResult);
            browserToolbar.style.display = libraryActive && !detailActive ? '' : 'none';
            filtersPanel.style.display = detailActive ? '' : 'none';
            manageView.style.display = manageActive ? '' : 'none';
            finderView.style.display = finderActive ? '' : 'none';
            settingsView.style.display = settingsActive ? '' : 'none';
            if (libraryActive) {
                detailView.style.display = detailActive ? '' : 'none';
                libraryView.style.display = detailActive ? 'none' : '';
            } else if (finderActive) {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
                if (!state.finderLoading && !state.seasonMappings.length) {
                    loadSeasonMappings().catch(function () { });
                }
            } else if (manageActive) {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
                if (!state.mappingExplorerRows.length) loadAllSeasonMappings().catch(function () { });
                else renderMappingExplorer();
            } else if (settingsActive) {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
                loadSettings(false);
            } else {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
            }
        }

        function getScrollContainer() {
            var candidates = [page, page.closest('.scroller'), page.closest('.page'), page.closest('.skinHeader-withScroller'), document.scrollingElement].filter(Boolean);
            var active = candidates.find(function (candidate) { return Number(candidate.scrollTop) > 0; });
            if (active) return active;
            var scrollable = candidates.find(function (candidate) {
                if (candidate === document.body || candidate === document.documentElement) return false;
                var style = window.getComputedStyle(candidate);
                return /(auto|scroll)/.test(style.overflowY || '') && candidate.scrollHeight > candidate.clientHeight;
            });
            return scrollable || document.scrollingElement || window;
        }

        function getScrollPosition() {
            var container = getScrollContainer();
            var top = container === window || container === document.scrollingElement
                ? window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop
                : container.scrollTop;
            var viewportTop = container === window || container === document.scrollingElement ? 0 : container.getBoundingClientRect().top;
            var anchor = Array.prototype.find.call(itemGrid.querySelectorAll('.ats-item-card[data-item-id]'), function (card) {
                return card.getBoundingClientRect().bottom > viewportTop;
            });
            return { top: top, anchorId: anchor ? anchor.getAttribute('data-item-id') : '', anchorOffset: anchor ? anchor.getBoundingClientRect().top - viewportTop : 0 };
        }

        function restoreScrollPosition(scrollPos) {
            if (!scrollPos) return;
            var container = getScrollContainer();
            function currentTop() {
                return container === window || container === document.scrollingElement ? window.pageYOffset : container.scrollTop;
            }
            function setTop(top) {
                if (container === window || container === document.scrollingElement) {
                    window.scrollTo(0, top);
                    document.documentElement.scrollTop = top;
                    document.body.scrollTop = top;
                } else container.scrollTop = top;
            }
            setTop(scrollPos.top);
            requestAnimationFrame(function () {
                var anchor = scrollPos.anchorId && Array.prototype.find.call(itemGrid.querySelectorAll('.ats-item-card[data-item-id]'), function (card) {
                    return card.getAttribute('data-item-id') === scrollPos.anchorId;
                });
                if (!anchor) return;
                var viewportTop = container === window || container === document.scrollingElement ? 0 : container.getBoundingClientRect().top;
                setTop(currentTop() + anchor.getBoundingClientRect().top - viewportTop - scrollPos.anchorOffset);
            });
        }

        function scrollDetailToTop() {
            var container = getScrollContainer();
            if (container === window || container === document.scrollingElement) {
                window.scrollTo(0, 0);
                document.documentElement.scrollTop = 0;
                document.body.scrollTop = 0;
            } else container.scrollTop = 0;
        }

        function handleScroll() {
            if (state.browserObserver || state.itemsLoading) return;
            if (state.items.length >= state.browserTotalRecordCount) return;

            var indicator = itemGrid.querySelector('.ats-load-more-indicator');
            if (!indicator) return;

            var container = getScrollContainer();
            var containerHeight = container === window ? window.innerHeight : container.clientHeight;
            var rect = indicator.getBoundingClientRect();

            if (rect.top <= containerHeight + 300) {
                loadItems(true);
            }
        }

        var scrollListenerAttached = false;
        var scrollListenerContainer = null;
        function attachScrollListener() {
            if (window.IntersectionObserver) return;
            var container = getScrollContainer();
            if (scrollListenerAttached && scrollListenerContainer === container) return;
            if (scrollListenerContainer) scrollListenerContainer.removeEventListener('scroll', handleScroll);
            container.addEventListener('scroll', handleScroll);
            scrollListenerAttached = true;
            scrollListenerContainer = container;
        }

        function observeBrowserSentinel(sentinel) {
            if (state.browserObserver) state.browserObserver.disconnect();
            state.browserObserver = null;
            if (!sentinel || state.items.length >= state.browserTotalRecordCount || !window.IntersectionObserver) return;
            state.browserObserver = new IntersectionObserver(function (entries) {
                if (entries.some(function (entry) { return entry.isIntersecting; }) && !state.itemsLoading) loadItems(true);
            }, { root: null, rootMargin: '300px 0px' });
            state.browserObserver.observe(sentinel);
        }

        function openLibraryView() {
            state.detailToken++;
            hideDetailLoading();
            detailView.style.display = 'none';
            libraryView.style.display = state.activeTab === 'library' ? '' : 'none';
            state.currentItem = null;
            state.currentResult = null;
            state.activeGroupId = null;
            state.detailLoading = false;
            state.detailError = null;
            detailView.classList.remove('ats-detail-loading');
            itemSelect.value = '';
            syncLayout();
            if (state.libraryScrollTop) {
                requestAnimationFrame(function () {
                    restoreScrollPosition(state.libraryScrollTop);
                    state.libraryScrollTop = null;
                });
            }
        }

        function openItemDetail(itemId) {
            if (!itemId) return;
            state.libraryScrollTop = getScrollPosition();
            itemSelect.value = itemId;
            state.currentItem = selectedItem(itemId);
            state.currentResult = null;
            state.activeGroupId = null;
            state.detailLoading = true;
            state.detailError = null;
            state.detailToken++;
            setActiveTab('library');
            showDetailLoading();
            renderDetailLoading();
            syncLayout();

            scrollDetailToTop();
            requestAnimationFrame(function () {
                if (state.detailLoading) scrollDetailToTop();
                requestAnimationFrame(function () { if (state.detailLoading) scrollDetailToTop(); });
            });

            loadThemes(state.detailToken);
        }

        function browserSortBy() {
            var sort = librarySort ? librarySort.value : 'name';
            if (sort === 'type') return 'ItemType';
            if (sort === 'saved') return 'saved';
            if (sort === 'size') return 'ThemeBytes';
            if (sort === 'link') return 'LinkStatus';
            if (sort === 'itemAdded') return 'DateCreatedUtc';
            if (sort === 'latestEpisodeAdded') return 'LatestEpisodeDateUtc';
            return 'SortName';
        }

        function browserItemsPath(startIndex) {
            var params = [
                ['startIndex', startIndex || 0],
                ['limit', state.browserLimit],
                ['sortBy', browserSortBy()],
                ['sortOrder', librarySortDirection && librarySortDirection.value === 'desc' ? 'Descending' : 'Ascending'],
                ['searchTerm', searchInput.value.trim()],
                ['itemType', libraryTypeFilter ? libraryTypeFilter.value : 'all'],
                ['linkFilter', libraryLinkFilter ? libraryLinkFilter.value : 'all'],
                ['savedFilter', librarySavedFilter ? librarySavedFilter.value : 'all']
            ].filter(function (pair) { return pair[1] !== null && pair[1] !== undefined && String(pair[1]).length; });
            return 'AnimeThemesSync/Items?' + params.map(function (pair) {
                return encodeURIComponent(pair[0]) + '=' + encodeURIComponent(pair[1]);
            }).join('&');
        }

        function loadItems(append, options) {
            options = options || {};
            if (append && state.itemsLoading) return Promise.resolve(state.items);
            var silent = options.silent === true;
            var startIndex = append ? state.items.length : 0;
            var targetCount = !append && options.preserveCount === true ? Math.max(state.items.length, state.browserLimit) : state.browserLimit;
            var token = ++state.browserRequestToken;
            state.itemsLoading = true;
            state.summaryLoading = true;
            if (!append && !silent) renderLibrarySkeleton();
            if (!silent) {
                renderSummarySkeleton();
                Dashboard.showLoadingMsg();
            }
            function fetchBrowserItems(accumulated) {
                return apiGet(browserItemsPath(startIndex + accumulated.length)).then(function (result) {
                    var rows = value(result, 'Items', 'items') || [];
                    accumulated = accumulated.concat(rows);
                    var total = Number(value(result, 'TotalRecordCount', 'totalRecordCount') || accumulated.length);
                    if (rows.length && accumulated.length < targetCount && startIndex + accumulated.length < total) return fetchBrowserItems(accumulated);
                    result.Items = result.items = accumulated;
                    return result;
                });
            }
            return Promise.all([fetchBrowserItems([]), apiGet('AnimeThemesSync/Summary'), apiGet('AnimeThemesSync/Storage')]).then(function (results) {
                if (token !== state.browserRequestToken) return state.items;
                var page = results[0] || {};
                var items = value(page, 'Items', 'items') || (Array.isArray(page) ? page : []);
                var summary = results[1] || {};
                var storage = results[2] || {};
                state.items = append ? state.items.concat(items || []) : (items || []);
                state.browserTotalRecordCount = Number(value(page, 'TotalRecordCount', 'totalRecordCount') || state.items.length || 0);
                state.browserCacheVersion = String(value(page, 'CacheVersion', 'cacheVersion') || '');
                state.browserCacheReady = !!value(page, 'CacheReady', 'cacheReady');
                state.browserRebuildRunning = !!value(storage, 'RebuildRunning', 'rebuildRunning');
                state.itemsLoading = false;
                state.summaryLoading = false;
                renderItemOptions(append);
                renderSummary(summary);
                renderStorage(storage);
                attachScrollListener();
                scheduleBrowserRefresh();
                if (!silent) Dashboard.hideLoadingMsg();
                return state.items;
            }).catch(function (err) {
                if (token !== state.browserRequestToken) return state.items;
                state.itemsLoading = false;
                state.summaryLoading = false;
                if (!append && !silent) {
                    itemGrid.innerHTML = '';
                    appendEmptyState(itemGrid, 'Library failed to load', getErrorMessage(err));
                }
                if (!silent) {
                    Dashboard.hideLoadingMsg();
                    Dashboard.alert({ title: 'Browser Error', message: 'Failed to load items: ' + getErrorMessage(err) });
                } else {
                    console.error('Failed to refresh Browser items', err);
                }
                return state.items;
            });
        }

        function scheduleLoadItems() {
            if (state.librarySearchTimer) {
                clearTimeout(state.librarySearchTimer);
            }

            state.librarySearchTimer = setTimeout(function () {
                loadItems(false);
            }, 250);
        }

        function postMaintenance(path, title) {
            Dashboard.showLoadingMsg();
            apiPost(path).then(function (result) {
                Dashboard.hideLoadingMsg();
                Dashboard.alert({ title: title, message: value(result || {}, 'Message', 'message') || 'Done.' });
                scheduleUiRefresh({ finder: true, mappings: true });
            }).catch(function (err) {
                Dashboard.hideLoadingMsg();
                Dashboard.alert({ title: title, message: getErrorMessage(err) });
            });
        }

        function scheduleBrowserRefresh() {
            if (state.browserRefreshTimer) {
                clearTimeout(state.browserRefreshTimer);
                state.browserRefreshTimer = null;
            }

            if (state.browserRebuildRunning || !state.browserCacheReady) {
                state.browserRefreshTimer = setTimeout(function () {
                    loadItems(false);
                }, 3000);
            }
        }

        function seasonRowId(row) {
            return value(row, 'SeasonItemId', 'seasonItemId');
        }

        function seasonFinderPath(startIndex) {
            var params = [
                ['startIndex', startIndex || 0],
                ['limit', state.finderLimit],
                ['searchTerm', seasonSearch.value.trim()],
                ['status', seasonFilter.value || 'unmatched'],
                ['sortBy', seasonSort.value || 'seriesName'],
                ['sortOrder', seasonSortDirection.value || 'asc']
            ].filter(function (pair) { return String(pair[1] === null || pair[1] === undefined ? '' : pair[1]).length; });
            return 'AnimeThemesSync/SeasonFinder?' + params.map(function (pair) {
                return encodeURIComponent(pair[0]) + '=' + encodeURIComponent(pair[1]);
            }).join('&');
        }

        function loadSeasonMappings(append, options) {
            options = options || {};
            if (append && state.finderLoading) return Promise.resolve(state.seasonMappings);
            var previousCount = state.seasonMappings.length;
            var preserve = !append && options.preserve === true;
            var selectedId = options.selectedId || (state.selectedSeason ? seasonRowId(state.selectedSeason) : null);
            var savedScrollTop = preserve ? state.finderScrollTop : 0;
            var token = append ? state.finderRequestToken : ++state.finderRequestToken;
            var targetCount = append ? previousCount + state.finderLimit : (preserve ? Math.max(previousCount, state.finderLimit) : state.finderLimit);
            var accumulated = append ? state.seasonMappings.slice() : [];
            var appendFrom = append ? previousCount : null;
            state.finderLoading = true;
            state.finderLoadError = null;
            if (state.finderObserver) state.finderObserver.disconnect();
            finderState.textContent = append ? 'Loading more seasons...' : (preserve ? 'Refreshing season mappings...' : 'Loading season mappings...');
            if (!append && !preserve) {
                state.seasonMappings = [];
                state.finderScrollTop = 0;
                renderFinderSkeleton(seasonList);
            }

            function fetchNextPage() {
                return apiGet(seasonFinderPath(accumulated.length)).then(function (result) {
                    if (token !== state.finderRequestToken) return false;
                    var rows = value(result, 'Items', 'items') || [];
                    accumulated = accumulated.concat(rows);
                    state.finderTotalRecordCount = Number(value(result, 'TotalRecordCount', 'totalRecordCount') || 0);
                    state.finderCacheVersion = String(value(result, 'CacheVersion', 'cacheVersion') || '');
                    state.finderCacheReady = value(result, 'CacheReady', 'cacheReady') !== false;
                    if (rows.length && accumulated.length < targetCount && accumulated.length < state.finderTotalRecordCount) {
                        return fetchNextPage();
                    }
                    return true;
                });
            }

            return fetchNextPage().then(function (accepted) {
                if (!accepted || token !== state.finderRequestToken) return state.seasonMappings;
                state.finderLoading = false;
                state.seasonMappings = accumulated;
                state.selectedSeason = selectedId ? state.seasonMappings.find(function (row) {
                    return String(seasonRowId(row)) === String(selectedId);
                }) || null : state.selectedSeason;
                state.finderScrollTop = preserve ? savedScrollTop : state.finderScrollTop;
                if (!state.finderCacheReady && !state.seasonMappings.length) {
                    renderFinderSkeleton(seasonList);
                    finderState.textContent = 'Season Finder cache is being built...';
                } else {
                    renderSeasonMappings(appendFrom);
                    finderState.textContent = state.seasonMappings.length + ' of ' + state.finderTotalRecordCount + ' seasons loaded.';
                }
                if (!state.finderCacheReady) scheduleFinderPoll();
                return state.seasonMappings;
            }).catch(function (err) {
                if (token !== state.finderRequestToken) return state.seasonMappings;
                state.finderLoading = false;
                state.finderLoadError = err;
                renderSeasonMappings();
                finderState.textContent = 'Failed to load season mappings.';
                return state.seasonMappings;
            });
        }

        function reloadSeasonMappingsPreservingState(selectedId) {
            return loadSeasonMappings(false, { preserve: true, selectedId: selectedId });
        }

        function loadAllSeasonMappings() {
            return apiGet('AnimeThemesSync/SeasonMappings').then(function (rows) {
                state.mappingExplorerRows = rows || [];
                renderMappingExplorer();
                return state.mappingExplorerRows;
            });
        }

        function scheduleFinderPoll() {
            if (state.finderPollTimer || state.activeTab !== 'finder') return;
            state.finderPollTimer = setTimeout(function () {
                state.finderPollTimer = null;
                if (state.activeTab === 'finder') reloadSeasonMappingsPreservingState();
            }, 2000);
        }

        function observeFinderSentinel(sentinel) {
            if (state.finderObserver) state.finderObserver.disconnect();
            if (!sentinel || state.seasonMappings.length >= state.finderTotalRecordCount) return;
            state.finderObserver = new IntersectionObserver(function (entries) {
                if (entries.some(function (entry) { return entry.isIntersecting; }) && !state.finderLoading) {
                    loadSeasonMappings(true);
                }
            }, { root: seasonList, rootMargin: '300px 0px' });
            state.finderObserver.observe(sentinel);
        }

        function renderSeasonMappings(appendFrom) {
            syncSeasonFilterButtons();
            var scrollTop = state.finderScrollTop;
            if (appendFrom === null || appendFrom === undefined) {
                seasonList.innerHTML = '';
            } else {
                var previousTail = seasonList.querySelector('.ats-finder-sentinel, .ats-finder-retry');
                if (previousTail) previousTail.remove();
            }
            if (!state.seasonMappings.length && state.finderLoading) {
                renderFinderSkeleton(seasonList);
                return;
            }
            if (!state.seasonMappings.length && !state.finderLoadError) {
                appendEmptyState(seasonList, 'No seasons match this filter', 'Switch filters or refresh season mappings.');
                return;
            }
            state.seasonMappings.slice(appendFrom || 0).forEach(function (row) {
                seasonList.appendChild(createSeasonCard(row));
            });
            if (state.finderLoadError) {
                var retry = createButton('Retry loading seasons', true);
                retry.classList.add('ats-finder-retry');
                retry.addEventListener('click', function () { loadSeasonMappings(state.seasonMappings.length > 0); });
                seasonList.appendChild(retry);
            } else if (state.seasonMappings.length < state.finderTotalRecordCount) {
                var sentinel = document.createElement('div');
                sentinel.className = 'ats-finder-sentinel';
                sentinel.setAttribute('aria-label', 'Loading more seasons');
                seasonList.appendChild(sentinel);
                observeFinderSentinel(sentinel);
            }
            requestAnimationFrame(function () { seasonList.scrollTop = scrollTop; });
        }

        function createSeasonCard(row) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-season-card ats-fade-in';
            button.classList.toggle('selected', state.selectedSeason && value(state.selectedSeason, 'SeasonItemId', 'seasonItemId') === value(row, 'SeasonItemId', 'seasonItemId'));
            var titleLine = document.createElement('div');
            titleLine.className = 'ats-season-card-title';
            appendDiv(titleLine, 'ats-season-series', text(value(row, 'SeriesName', 'seriesName')));
            appendDiv(titleLine, 'ats-season-name', text(value(row, 'SeasonName', 'seasonName')));
            button.appendChild(titleLine);
            appendDiv(button, 'ats-item-meta', [
                'Season ' + text(value(row, 'SeasonNumber', 'seasonNumber')),
                text(value(row, 'AnimeName', 'animeName') || value(row, 'AnimeThemesSlug', 'animeThemesSlug') || 'Needs match')
            ].join(' | '));
            var chips = document.createElement('div');
            chips.className = 'ats-status-list';
            var hasMatch = seasonMappingHasMatch(row);
            addChip(chips, text(value(row, 'Status', 'status')), hasMatch ? 'ok' : 'missing');
            addChip(chips, text(value(row, 'Source', 'source')), '');
            if (value(row, 'SameAsSeries', 'sameAsSeries')) addChip(chips, 'Series-level', 'ok');
            if (!hasMatch) addChip(chips, 'Needs match', 'missing');
            button.appendChild(chips);
            button.addEventListener('click', function () {
                selectSeason(row);
            });
            return button;
        }

        function selectSeason(row) {
            state.selectedSeason = row;
            state.selectedAnime = null;
            state.finderPreview = null;
            finderSearchInput.value = value(row, 'SeriesName', 'seriesName') || '';
            finderYear.value = '';
            finderResults.innerHTML = '';
            finderPreview.className = 'ats-finder-preview fieldDescription';
            finderPreview.textContent = 'Search AnimeThemes and select a candidate for ' + text(value(row, 'SeasonName', 'seasonName')) + '.';
            renderSeasonMappings();
            var seasonId = value(row, 'SeasonItemId', 'seasonItemId');
            if (!state.finderAutoSearched[seasonId]) {
                state.finderAutoSearched[seasonId] = true;
                scheduleAnimeThemesSearch(250);
            } else {
                finderState.textContent = 'Ready. Use Search to refresh candidates.';
            }
        }

        function openFinderForSeasonGroup(group) {
            var targetGroup = resolveFinderTargetGroup(group);
            if (!targetGroup) {
                return;
            }

            var seasonItemId = value(targetGroup, 'SeasonItemId', 'seasonItemId') || value(targetGroup, 'ItemId', 'itemId');
            if (!seasonItemId) {
                return;
            }

            var targetStatus = String(value(targetGroup, 'Status', 'status') || '').toLowerCase();
            seasonFilter.value = targetStatus === 'unmatched' ? 'unmatched' : 'all';
            syncSeasonFilterButtons();
            setActiveTab('finder');
            loadAllSeasonMappings().then(function (rows) {
                var row = (rows || []).find(function (candidate) {
                    return String(seasonRowId(candidate)) === String(seasonItemId);
                });
                if (!row) {
                    finderState.textContent = 'Season was not found in Season Finder.';
                    return;
                }
                if (!state.seasonMappings.some(function (candidate) { return String(seasonRowId(candidate)) === String(seasonItemId); })) {
                    state.seasonMappings.unshift(row);
                    state.finderTotalRecordCount = Math.max(state.finderTotalRecordCount, state.seasonMappings.length);
                }
                selectSeason(row);
            }).catch(function () {
                // loadSeasonMappings already surfaced the error.
            });
        }

        function resolveFinderTargetGroup(group) {
            group = group || activeGroup();
            if (!group) return null;
            if (String(value(group, 'Type', 'type')) === 'Season') {
                return group;
            }

            if (String(value(group, 'Type', 'type')) !== 'Series') {
                return null;
            }

            var groups = getGroups().filter(function (candidate) {
                return String(value(candidate, 'Type', 'type')) === 'Season';
            });
            return groups.find(function (candidate) {
                return Number(value(candidate, 'SeasonNumber', 'seasonNumber')) === 1;
            }) || groups[0] || null;
        }

        function isFinderMatchActionAvailable(group) {
            var targetGroup = resolveFinderTargetGroup(group);
            if (!targetGroup) return false;
            return String(value(targetGroup, 'Status', 'status') || '').toLowerCase() === 'unmatched';
        }

        function appendEmptyMatchAction(container, group) {
            if (!isFinderMatchActionAvailable(group)) return;
            var actions = document.createElement('div');
            actions.className = 'ats-empty-actions';
            var button = createButton('Match in Season Finder', false, 'link');
            button.addEventListener('click', function () {
                openFinderForSeasonGroup(group);
            });
            actions.appendChild(button);
            container.appendChild(actions);
        }

        function syncMatchInFinderButton() {
            if (!matchInFinderButton) return;
            matchInFinderButton.disabled = !resolveFinderTargetGroup(activeGroup());
        }

        function scheduleAnimeThemesSearch(delay) {
            if (state.finderSearchTimer) {
                clearTimeout(state.finderSearchTimer);
            }

            state.finderSearchTimer = setTimeout(function () {
                searchAnimeThemes(true);
            }, delay || 350);
        }

        function searchAnimeThemes(silent) {
            var query = finderSearchInput.value.trim();
            if (!query) {
                finderResults.innerHTML = '';
                if (!silent) {
                    Dashboard.alert({ title: 'Season Finder', message: 'Enter a title to search.' });
                }
                return;
            }

            var year = finderYear.value ? parseInt(finderYear.value, 10) : null;
            var token = ++state.finderSearchToken;
            finderState.textContent = 'Searching AnimeThemes...';
            renderFinderSkeleton(finderResults);
            apiGet('AnimeThemesSync/Search?query=' + encodeURIComponent(query) + (year ? '&year=' + encodeURIComponent(year) : '')).then(function (results) {
                if (token !== state.finderSearchToken) return;
                renderSearchResults(results || []);
                finderState.textContent = (results || []).length + ' candidates found.';
            }).catch(function (err) {
                if (token !== state.finderSearchToken) return;
                finderResults.innerHTML = '';
                appendEmptyState(finderResults, 'Search failed', getErrorMessage(err));
                finderState.textContent = 'Search failed.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
        }

        function renderSearchResults(results) {
            finderResults.innerHTML = '';
            if (!results.length) {
                appendEmptyState(finderResults, 'No candidates found', 'Try the main series title or remove the year filter.');
                return;
            }

            results.forEach(function (result) {
                finderResults.appendChild(createSearchCard(result));
            });
        }

        function createSearchCard(result) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-search-card ats-fade-in';
            button.classList.toggle('selected', state.selectedAnime && value(state.selectedAnime, 'Slug', 'slug') === value(result, 'Slug', 'slug'));
            var posterBox = document.createElement('div');
            posterBox.className = 'ats-candidate-poster';
            var posterImage = document.createElement('img');
            posterImage.alt = '';
            var posterFallback = document.createElement('span');
            posterFallback.textContent = String(value(result, 'Name', 'name') || 'AT').trim().slice(0, 2).toUpperCase();
            posterBox.appendChild(posterImage);
            posterBox.appendChild(posterFallback);
            setImage(posterImage, value(result, 'PrimaryImageUrl', 'primaryImageUrl'), posterFallback);
            button.appendChild(posterBox);
            var body = document.createElement('div');
            appendDiv(body, 'ats-item-title', text(value(result, 'Name', 'name')));
            var matchedTitle = value(result, 'MatchedTitle', 'matchedTitle');
            if (matchedTitle) {
                var matchedType = value(result, 'MatchedTitleType', 'matchedTitleType');
                appendDiv(body, 'ats-item-meta', 'Matched' + (matchedType ? ' (' + text(matchedType) + ')' : '') + ': ' + text(matchedTitle));
            }
            appendDiv(body, 'ats-item-meta', [
                value(result, 'Year', 'year'),
                value(result, 'Season', 'season'),
                value(result, 'MediaFormat', 'mediaFormat'),
                value(result, 'Slug', 'slug'),
                'score ' + text(value(result, 'Score', 'score'))
            ].filter(Boolean).join(' | '));
            button.appendChild(body);
            button.addEventListener('click', function () {
                selectAnimeCandidate(result);
            });
            return button;
        }

        function selectAnimeCandidate(result) {
            var slug = value(result, 'Slug', 'slug');
            if (!slug) return;
            state.selectedAnime = result;
            state.finderPreview = null;
            finderState.textContent = 'Loading themes for ' + text(value(result, 'Name', 'name')) + '...';
            finderPreview.className = 'ats-finder-preview';
            renderFinderSkeleton(finderPreview);
            apiGet('AnimeThemesSync/Anime/' + encodeURIComponent(slug) + '/Themes').then(function (preview) {
                state.finderPreview = preview || {};
                renderFinderPreview();
                finderState.textContent = 'Preview loaded.';
            }).catch(function (err) {
                finderPreview.innerHTML = '';
                appendEmptyState(finderPreview, 'Preview failed to load', getErrorMessage(err));
                finderState.textContent = 'Failed to load preview.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
            searchAnimeThemesHighlight();
        }

        function searchAnimeThemesHighlight() {
            Array.prototype.forEach.call(finderResults.children, function (node) {
                if (!state.selectedAnime) return;
                node.classList.toggle('selected', node.querySelector('.ats-item-meta') && node.textContent.indexOf(value(state.selectedAnime, 'Slug', 'slug')) !== -1);
            });
        }

        function renderFinderPreview() {
            var preview = state.finderPreview || {};
            var anime = state.selectedAnime || {};
            var rows = value(preview, 'Themes', 'themes') || [];
            finderPreview.className = 'ats-finder-preview';
            finderPreview.innerHTML = '';
            var head = document.createElement('div');
            head.className = 'ats-finder-preview-head';
            appendDiv(head, 'ats-item-title', text(value(anime, 'Name', 'name')));
            appendDiv(head, 'ats-item-meta', [
                value(anime, 'Year', 'year'),
                value(anime, 'Season', 'season'),
                value(anime, 'Slug', 'slug'),
                rows.length + ' themes'
            ].filter(Boolean).join(' | '));
            var actions = document.createElement('div');
            actions.className = 'ats-actions';
            addFinderAction(actions, 'Save mapping', false, function () { saveSeasonMapping(false); });
            addFinderAction(actions, 'Save & Download', false, function () { saveSeasonMapping(true); });
            addFinderAction(actions, 'Clear mapping', true, clearSeasonMapping, 'danger');
            addOpenButton(actions, 'AnimeThemes', value(anime, 'AnimeThemesUrl', 'animeThemesUrl'));
            head.appendChild(actions);
            finderPreview.appendChild(head);

            var list = document.createElement('div');
            list.className = 'ats-finder-preview-list';
            if (!rows.length) {
                appendEmptyState(list, 'No themes found', 'This candidate has no previewable themes.');
            } else {
                rows.slice(0, 12).forEach(function (row) {
                    list.appendChild(createPreviewThemeRow(row));
                });
            }
            finderPreview.appendChild(list);
        }

        function createPreviewThemeRow(row) {
            var card = document.createElement('div');
            card.className = 'ats-theme-card ats-fade-in';
            var main = document.createElement('div');
            main.className = 'ats-theme-main';
            appendDiv(main, 'ats-theme-key', padOrder(value(row, 'Order', 'order')) + ' - ' + text(value(row, 'ThemeKey', 'themeKey')));
            appendDiv(main, 'ats-song', text(value(row, 'SongTitle', 'songTitle')));
            appendDiv(main, 'ats-artist fieldDescription', text(value(row, 'Artists', 'artists')));
            card.appendChild(main);
            var detail = document.createElement('div');
            detail.className = 'ats-detail';
            appendDiv(detail, 'fieldDescription', 'Episodes: ' + text(value(row, 'Episodes', 'episodes')));
            appendDiv(detail, 'fieldDescription', 'Quality: ' + text(value(row, 'Quality', 'quality')));
            card.appendChild(detail);
            var actions = document.createElement('div');
            actions.className = 'ats-actions';
            addRemotePreviewButton(actions, row, 'video');
            addRemotePreviewButton(actions, row, 'audio');
            card.appendChild(actions);
            return card;
        }

        function addFinderAction(container, label, secondary, handler, tone) {
            var button = createButton(label, secondary, tone);
            button.disabled = !state.selectedSeason || !state.selectedAnime;
            if (label === 'Clear mapping') {
                button.disabled = !state.selectedSeason;
            }
            button.addEventListener('click', handler);
            container.appendChild(button);
        }

        function addRemotePreviewButton(container, row, target, isSmall) {
            var url = target === 'audio' ? value(row, 'AudioUrl', 'audioUrl') : value(row, 'VideoUrl', 'videoUrl');
            var button = createButton(target === 'audio' ? 'Preview Audio' : 'Preview Video', true, 'play');
            if (isSmall) {
                button.classList.add('ats-btn-preview-small');
                var labelSpan = button.querySelector('span:not(.ats-icon)');
                if (labelSpan) labelSpan.textContent = 'Preview';
            }
            button.disabled = !url;
            button.addEventListener('click', function () {
                if (url) openRemotePlayer(row, target, url);
            });
            container.appendChild(button);
        }

        function saveSeasonMapping(downloadAfterSave) {
            if (!state.selectedSeason || !state.selectedAnime) return;
            var payload = {
                SeasonItemId: value(state.selectedSeason, 'SeasonItemId', 'seasonItemId'),
                AnimeThemesSlug: value(state.selectedAnime, 'Slug', 'slug'),
                AniListId: value(state.selectedAnime, 'AniListId', 'aniListId'),
                MyAnimeListId: value(state.selectedAnime, 'MyAnimeListId', 'myAnimeListId'),
                Locked: true
            };
            finderState.textContent = 'Saving mapping...';
            apiPostJson('AnimeThemesSync/SeasonMappings', payload).then(function (row) {
                state.selectedSeason = row || state.selectedSeason;
                state.mappingExplorerRows = [];
                scheduleUiRefresh({ finder: true, mappings: true });
                if (downloadAfterSave) {
                    startDownloadBatch({
                        path: 'AnimeThemesSync/Jobs/ItemDownloadBatch?ItemId=' + encodeURIComponent(payload.SeasonItemId) + '&Force=false',
                        itemId: payload.SeasonItemId,
                        title: [value(state.selectedSeason, 'SeriesName', 'seriesName'), value(state.selectedSeason, 'SeasonName', 'seasonName')].filter(Boolean).join(' / ') || 'Season download'
                    });
                } else {
                    finderState.textContent = 'Mapping saved.';
                }
            }).catch(function (err) {
                finderState.textContent = 'Save failed.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
        }

        function clearSeasonMapping() {
            if (!state.selectedSeason) return;
            var seasonId = value(state.selectedSeason, 'SeasonItemId', 'seasonItemId');
            finderState.textContent = 'Clearing mapping...';
            apiDelete('AnimeThemesSync/SeasonMappings/' + encodeURIComponent(seasonId)).then(function (row) {
                state.selectedSeason = row || null;
                state.mappingExplorerRows = [];
                state.selectedAnime = null;
                state.finderPreview = null;
                finderPreview.className = 'ats-finder-preview fieldDescription';
                finderPreview.textContent = 'Mapping cleared.';
                scheduleUiRefresh({ finder: true, mappings: true });
            }).catch(function (err) {
                finderState.textContent = 'Clear failed.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
        }

        function renderSummary(summary) {
            summaryItems.textContent = text(value(summary, 'Items', 'items'));
            summarySeriesItems.textContent = text(value(summary, 'SeriesItems', 'seriesItems'));
            summaryMovieItems.textContent = text(value(summary, 'MovieItems', 'movieItems'));
            summarySeasonItems.textContent = text(value(summary, 'SeasonItems', 'seasonItems'));
            summarySavedItems.textContent = text(value(summary, 'SavedItems', 'savedItems'));
            summaryVideos.textContent = text(value(summary, 'ThemeVideos', 'themeVideos'));
            summarySongs.textContent = text(value(summary, 'ThemeSongs', 'themeSongs'));
            summaryExtras.textContent = text(value(summary, 'Extras', 'extras'));
            summaryBytes.textContent = formatBytes(value(summary, 'TotalBytes', 'totalBytes'));
            summaryManualSeasonMappings.textContent = text(value(summary, 'ManualSeasonMappings', 'manualSeasonMappings'));
            summaryAutoSeasonMappings.textContent = text(value(summary, 'AutoSeasonMappings', 'autoSeasonMappings'));
            summaryDirectSeasonMappings.textContent = text(value(summary, 'DirectSeasonMappings', 'directSeasonMappings'));
            summarySeriesSharedSeasons.textContent = text(value(summary, 'SeriesSharedSeasons', 'seriesSharedSeasons'));
            summaryUnmatchedSeasons.textContent = text(value(summary, 'UnmatchedSeasons', 'unmatchedSeasons'));
        }

        function renderStorage(storage) {
            if (!storage) return;
            var ready = !!value(storage, 'CacheReady', 'cacheReady');
            var rebuilding = !!value(storage, 'RebuildRunning', 'rebuildRunning');
            var finderStorage = value(storage, 'SeasonFinder', 'seasonFinder');
            if (cacheBytes) cacheBytes.textContent = formatBytes(value(storage, 'DatabaseBytes', 'databaseBytes'));
            if (cacheItems) cacheItems.textContent = text(value(storage, 'BrowserItemCount', 'browserItemCount')) + ' browser / ' + text(value(finderStorage, 'ItemCount', 'itemCount')) + ' seasons';
            if (cacheState) cacheState.textContent = rebuilding ? 'Updating' : (ready ? 'Ready' : 'Starting');
            if (cachePath) {
                var path = value(storage, 'DatabasePath', 'databasePath');
                var lastError = value(storage, 'LastError', 'lastError');
                var finderPath = value(finderStorage, 'DatabasePath', 'databasePath');
                cachePath.textContent = (lastError ? ('Cache file: ' + path + ' | Last error: ' + lastError) : ('Cache file: ' + path)) + (finderPath ? (' | Season Finder DB: ' + finderPath) : '');
            }
        }

        function setImportState(message) {
            if (importState) {
                importState.textContent = message || 'Ready.';
            }
        }

        function renderImportPreview(config) {
            importPreview.textContent = [
                'Ready to import settings.',
                'ConfigurationVersion: ' + text(value(config, 'ConfigurationVersion', 'configurationVersion')),
                'Downloads: ' + (value(config, 'ThemeDownloadingEnabled', 'themeDownloadingEnabled') ? 'enabled' : 'disabled'),
                'Season downloads: ' + (value(config, 'SeasonThemeDownloadsEnabled', 'seasonThemeDownloadsEnabled') ? 'enabled' : 'disabled')
            ].join('\n');
        }

        function parseImportJson(showAlerts) {
            var raw = importJsonInput.value.trim();
            state.importConfig = null;
            importApplyButton.disabled = true;
            if (!raw) {
                importPreview.textContent = 'No import loaded.';
                setImportState('Ready.');
                return null;
            }

            try {
                var parsed = JSON.parse(raw);
                if (!parsed || Array.isArray(parsed) || typeof parsed !== 'object') {
                    throw new Error('PluginConfiguration JSON must be an object.');
                }

                var normalized = canonicalizeFullSettings(parsed);
                removeLegacySettings(normalized);
                state.importConfig = normalized;
                importJsonInput.value = serializeFullSettings(normalized);
                renderImportPreview(normalized);
                importApplyButton.disabled = false;
                setImportState('Import ready.');
                return normalized;
            } catch (err) {
                importPreview.textContent = 'Import error: ' + getErrorMessage(err);
                setImportState('Import invalid.');
                if (showAlerts) {
                    Dashboard.alert({ title: 'Import Error', message: getErrorMessage(err) });
                }
                return null;
            }
        }

        function loadCurrentConfigForImport() {
            setImportState('Loading current configuration...');
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                var normalized = canonicalizeFullSettings(config || {});
                importJsonInput.value = serializeFullSettings(normalized);
                parseImportJson(false);
            }).catch(function (err) {
                setImportState('Failed to load configuration.');
                Dashboard.alert({ title: 'Import Error', message: getErrorMessage(err) });
            });
        }

        function downloadJsonFile(fileName, content) {
            var blob = new Blob([content], { type: 'application/json' });
            var url = URL.createObjectURL(blob);
            var link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }

        function exportConfig() {
            setImportState('Exporting settings...');
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                var content = serializeFullSettings(config || {});
                importJsonInput.value = content;
                parseImportJson(false);
                downloadJsonFile('animethemes-sync-settings.json', content);
                setImportState('Settings exported.');
            }).catch(function (err) {
                setImportState('Export failed.');
                Dashboard.alert({ title: 'Export Error', message: getErrorMessage(err) });
            });
        }

        function applyImportConfig() {
            var config = state.importConfig || parseImportJson(true);
            if (!config) return;
            setImportState('Saving imported settings...');
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (current) {
                current = ensureSettingsConfig(current || {});
                Object.keys(config).forEach(function (key) {
                    if (key !== 'SeasonThemeMappings') {
                        current[key] = config[key];
                    }
                });
                return ApiClient.updatePluginConfiguration(pluginUniqueId, current).then(function (result) {
                    return { result: result, config: current };
                });
            }).then(function (saved) {
                var result = saved.result;
                var config = saved.config;
                state.settingsConfig = ensureSettingsConfig(config);
                state.settingsLoaded = true;
                applySettingsToForm(config || {});
                captureSettingsSnapshot();
                importApplyButton.disabled = true;
                setImportState('Settings import applied.');
                scheduleUiRefresh({ finder: true, mappings: true });
                Dashboard.processPluginConfigurationUpdateResult(result);
            }).catch(function (err) {
                setImportState('Import save failed.');
                Dashboard.alert({ title: 'Import Error', message: getErrorMessage(err) });
            });
        }

        function handleImportFile() {
            var file = importFileInput.files && importFileInput.files[0];
            if (!file) return;
            var reader = new FileReader();
            reader.onload = function () {
                importJsonInput.value = String(reader.result || '');
                parseImportJson(true);
            };
            reader.onerror = function () {
                setImportState('Failed to read file.');
                Dashboard.alert({ title: 'Import Error', message: 'Failed to read the selected file.' });
            };
            reader.readAsText(file);
        }

        function setMappingsState(message) {
            if (mappingsState) {
                mappingsState.textContent = message || 'Ready.';
            }
        }

        function mappingImportRowsFromRaw(raw) {
            var parsed = JSON.parse(raw);
            var rows = Array.isArray(parsed) ? parsed : parsed && Array.isArray(parsed.Mappings) ? parsed.Mappings : null;
            if (!rows) {
                throw new Error('Mappings JSON must be an array or an object with a Mappings array.');
            }

            return rows.map(function (row) {
                return {
                    SeasonItemId: value(row, 'SeasonItemId', 'seasonItemId'),
                    AnimeThemesSlug: value(row, 'AnimeThemesSlug', 'animeThemesSlug'),
                    AniListId: value(row, 'AniListId', 'aniListId'),
                    MyAnimeListId: value(row, 'MyAnimeListId', 'myAnimeListId'),
                    Locked: value(row, 'Locked', 'locked') !== false,
                    Status: value(row, 'Status', 'status')
                };
            });
        }

        function parseMappingsJson(showAlerts) {
            state.mappingsImportRows = null;
            mappingsApplyButton.disabled = true;
            var raw = mappingsJsonInput.value.trim();
            if (!raw) {
                mappingsPreview.textContent = 'No mappings import loaded.';
                setMappingsState('Ready.');
                return null;
            }

            try {
                var rows = mappingImportRowsFromRaw(raw);
                var importable = rows.filter(function (row) {
                    return row.SeasonItemId && (row.AnimeThemesSlug || row.AniListId || row.MyAnimeListId);
                });
                state.mappingsImportRows = importable;
                mappingsPreview.textContent = 'Rows: ' + rows.length + '\nImportable: ' + importable.length + '\nSkipped: ' + (rows.length - importable.length);
                mappingsApplyButton.disabled = !importable.length;
                setMappingsState('Mappings import ready.');
                return importable;
            } catch (err) {
                mappingsPreview.textContent = 'Mappings import error: ' + getErrorMessage(err);
                setMappingsState('Mappings import invalid.');
                if (showAlerts) {
                    Dashboard.alert({ title: 'Mappings Import Error', message: getErrorMessage(err) });
                }
                return null;
            }
        }

        function exportMappings() {
            var doExport = function () {
                var rows = (state.mappingExplorerRows || []).filter(seasonMappingHasMatch).map(function (row) {
                    return {
                        SeriesItemId: value(row, 'SeriesItemId', 'seriesItemId'),
                        SeriesName: value(row, 'SeriesName', 'seriesName'),
                        SeasonItemId: value(row, 'SeasonItemId', 'seasonItemId'),
                        SeasonName: value(row, 'SeasonName', 'seasonName'),
                        SeasonNumber: value(row, 'SeasonNumber', 'seasonNumber'),
                        Status: value(row, 'Status', 'status'),
                        Source: value(row, 'Source', 'source'),
                        AnimeName: value(row, 'AnimeName', 'animeName'),
                        AnimeThemesSlug: value(row, 'AnimeThemesSlug', 'animeThemesSlug'),
                        AniListId: value(row, 'AniListId', 'aniListId'),
                        MyAnimeListId: value(row, 'MyAnimeListId', 'myAnimeListId'),
                        Locked: true
                    };
                });
                var content = JSON.stringify({ Mappings: rows }, null, 2);
                mappingsJsonInput.value = content;
                parseMappingsJson(false);
                downloadJsonFile('animethemes-sync-mappings.json', content);
                setMappingsState('Mappings exported.');
            };
            if (!state.mappingExplorerRows.length) {
                loadAllSeasonMappings().then(doExport);
            } else {
                doExport();
            }
        }

        function exportLibrarySnapshot() {
            var doExport = function () {
                var content = JSON.stringify({ Rows: state.mappingExplorerRows || [] }, null, 2);
                downloadJsonFile('animethemes-sync-library-snapshot.json', content);
                setMappingsState('Library snapshot exported.');
            };
            if (!state.mappingExplorerRows.length) {
                loadAllSeasonMappings().then(doExport);
            } else {
                doExport();
            }
        }

        function applyMappingsImport() {
            var rows = state.mappingsImportRows || parseMappingsJson(true);
            if (!rows || !rows.length) return;
            setMappingsState('Importing mappings...');
            apiPostJson('AnimeThemesSync/SeasonMappings/Import', { Mappings: rows }).then(function (result) {
                var imported = value(result, 'Imported', 'imported') || 0;
                var skipped = value(result, 'Skipped', 'skipped') || 0;
                var errors = value(result, 'Errors', 'errors') || [];
                mappingsPreview.textContent = 'Imported: ' + imported + '\nSkipped: ' + skipped + '\nErrors: ' + errors.length + (errors.length ? '\n' + errors.join('\n') : '');
                mappingsApplyButton.disabled = true;
                setMappingsState('Mappings import complete.');
                state.mappingExplorerRows = [];
                scheduleUiRefresh({ finder: true, mappings: true });
            }).catch(function (err) {
                setMappingsState('Mappings import failed.');
                Dashboard.alert({ title: 'Mappings Import Error', message: getErrorMessage(err) });
            });
        }

        function handleMappingsFile() {
            var file = mappingsFileInput.files && mappingsFileInput.files[0];
            if (!file) return;
            var reader = new FileReader();
            reader.onload = function () {
                mappingsJsonInput.value = String(reader.result || '');
                parseMappingsJson(true);
            };
            reader.onerror = function () {
                setMappingsState('Failed to read file.');
                Dashboard.alert({ title: 'Mappings Import Error', message: 'Failed to read the selected file.' });
            };
            reader.readAsText(file);
        }

        function renderMappingExplorer() {
            if (!explorerTable) return;
            var rows = state.mappingExplorerRows || [];
            var query = explorerSearch ? explorerSearch.value.trim().toLowerCase() : '';
            var statusFilterValue = explorerStatus ? explorerStatus.value : 'all';
            var filtered = rows.filter(function (row) {
                var status = String(value(row, 'Status', 'status') || '').toLowerCase();
                if (statusFilterValue !== 'all' && status !== statusFilterValue) {
                    return false;
                }

                if (!query) return true;
                var haystack = [
                    value(row, 'SeriesName', 'seriesName'),
                    value(row, 'SeasonName', 'seasonName'),
                    value(row, 'Status', 'status'),
                    value(row, 'Source', 'source'),
                    value(row, 'AnimeName', 'animeName'),
                    value(row, 'AnimeThemesSlug', 'animeThemesSlug'),
                    value(row, 'AniListId', 'aniListId'),
                    value(row, 'MyAnimeListId', 'myAnimeListId'),
                    value(row, 'SeriesPath', 'seriesPath'),
                    value(row, 'SeasonPath', 'seasonPath')
                ].join(' ').toLowerCase();
                return haystack.indexOf(query) !== -1;
            });

            explorerTable.innerHTML = '';
            if (!filtered.length) {
                appendEmptyState(explorerTable, 'No mappings found', rows.length ? 'Adjust the explorer filters.' : 'Refresh mappings to populate the explorer.');
                return;
            }

            var table = document.createElement('table');
            var thead = document.createElement('thead');
            var headRow = document.createElement('tr');
            ['Series', 'Season', 'Status', 'Source', 'AnimeThemes', 'AniList', 'MAL', 'Path', 'Action'].forEach(function (label) {
                var th = document.createElement('th');
                th.textContent = label;
                headRow.appendChild(th);
            });
            thead.appendChild(headRow);
            table.appendChild(thead);
            var tbody = document.createElement('tbody');
            filtered.forEach(function (row) {
                var tr = document.createElement('tr');
                [
                    value(row, 'SeriesName', 'seriesName'),
                    value(row, 'SeasonName', 'seasonName'),
                    value(row, 'Status', 'status'),
                    value(row, 'Source', 'source'),
                    value(row, 'AnimeThemesSlug', 'animeThemesSlug') || value(row, 'AnimeName', 'animeName'),
                    value(row, 'AniListId', 'aniListId'),
                    value(row, 'MyAnimeListId', 'myAnimeListId'),
                    value(row, 'SeasonPath', 'seasonPath') || value(row, 'SeriesPath', 'seriesPath')
                ].forEach(function (cellValue) {
                    var td = document.createElement('td');
                    td.textContent = text(cellValue);
                    tr.appendChild(td);
                });
                var actionCell = document.createElement('td');
                var button = createButton('Find match', true);
                button.disabled = seasonMappingHasMatch(row);
                button.addEventListener('click', function () {
                    seasonFilter.value = 'unmatched';
                    syncSeasonFilterButtons();
                    setActiveTab('finder');
                    selectSeason(row);
                });
                actionCell.appendChild(button);
                tr.appendChild(actionCell);
                tbody.appendChild(tr);
            });
            table.appendChild(tbody);
            explorerTable.appendChild(table);
        }

        function renderDetailLoading() {
            detailView.classList.add('ats-detail-loading');
            downloadItemButton.disabled = true;
            if (matchInFinderButton) matchInFinderButton.disabled = true;

            var item = state.currentItem || {};
            var name = value(item, 'Name', 'name') || 'Loading item...';
            title.textContent = name;

            var imageSrc = value(item, 'PrimaryImageUrl', 'primaryImageUrl') || value(item, 'ThumbImageUrl', 'thumbImageUrl');
            if (imageSrc) {
                poster.onload = function () {
                    poster.classList.remove('ats-loading-blur');
                    poster.onload = null;
                };
                poster.onerror = function () {
                    poster.classList.remove('ats-loading-blur');
                    poster.onerror = null;
                };
                poster.src = apiUrl(imageSrc, false);
                poster.style.display = 'block';
                poster.classList.add('ats-loading-blur');
                posterFallback.style.display = 'none';
            } else {
                poster.removeAttribute('src');
                poster.style.display = 'none';
                posterFallback.style.display = 'block';
                posterFallback.textContent = String(name).trim().slice(0, 2).toUpperCase();
            }

            var backdropSrc = value(item, 'BackdropImageUrl', 'backdropImageUrl') || value(item, 'ThumbImageUrl', 'thumbImageUrl') || value(item, 'PrimaryImageUrl', 'primaryImageUrl');
            if (backdropSrc) {
                setBackdrop(backdropSrc);
                var bg = page.querySelector('.ats-hero-bg') || backdrop;
                if (bg) bg.classList.add('ats-loading-blur');
            } else {
                setBackdrop(null);
            }

            logo.style.display = 'none';
            meta.innerHTML = '';
            for (var metaLine = 0; metaLine < 2; metaLine++) {
                var metaSkeleton = document.createElement('span');
                metaSkeleton.className = 'ats-skeleton ats-skeleton-text';
                if (metaLine) metaSkeleton.style.width = '2.75rem';
                meta.appendChild(metaSkeleton);
            }

            seasonGroups.style.display = '';
            seasonGroups.innerHTML = '';
            for (var i = 0; i < 2; i++) {
                var pill = document.createElement('div');
                pill.className = 'ats-season-pill ats-placeholder-card ats-skeleton-shimmer-only';
                pill.style.height = '2.25rem';
                pill.style.width = '6rem';
                seasonGroups.appendChild(pill);
            }
            rowsContainer.innerHTML = '';
            for (var row = 0; row < 3; row++) {
                var card = document.createElement('div');
                card.className = 'ats-theme-card ats-placeholder-card ats-skeleton-shimmer-only';
                card.style.height = '6.5rem';
                rowsContainer.appendChild(card);
            }
        }

        function renderDetailError(message) {
            detailView.classList.remove('ats-detail-loading');
            downloadItemButton.disabled = true;
            if (matchInFinderButton) matchInFinderButton.disabled = true;
            setBackdrop(null);
            poster.style.display = 'none';
            logo.style.display = 'none';
            posterFallback.style.display = '';
            posterFallback.textContent = '!';
            title.textContent = 'Unable to load item';
            meta.textContent = message || 'The selected item could not be loaded.';
            seasonGroups.innerHTML = '';
            seasonGroups.style.display = 'none';
            rowsContainer.innerHTML = '';
            appendEmptyState(rowsContainer, 'Themes could not be loaded', message || 'Go back to the library and try again.');
        }

        function loadThemes(token, options) {
            options = options || {};
            var silent = options.silent === true;
            var itemId = options.itemId || itemSelect.value || (state.currentItem ? value(state.currentItem, 'Id', 'id') : null);
            if (!itemId) return;
            state.currentItem = state.items.find(function (item) {
                return String(value(item, 'Id', 'id')) === String(itemId);
            }) || selectedItem(itemId);
            var previousGroupId = state.activeGroupId;
            return apiGet('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes').then(function (result) {
                if (token && token !== state.detailToken) return;
                hideDetailLoading(token);
                state.detailLoading = false;
                state.detailError = null;
                detailView.classList.remove('ats-detail-loading');
                downloadItemButton.disabled = false;
                state.currentResult = result || {};
                var groups = getGroups();
                state.activeGroupId = groups.some(function (group) { return groupId(group) === previousGroupId; })
                    ? previousGroupId
                    : selectDefaultGroup(groups);
                renderHero();
                renderSeasonGroups();
                renderThemes();
                syncLayout();
            }).catch(function (err) {
                if (token && token !== state.detailToken) return;
                hideDetailLoading(token);
                state.detailLoading = false;
                if (silent) {
                    console.error('Failed to refresh Browser detail', err);
                    return;
                }
                state.currentResult = null;
                state.detailError = getErrorMessage(err);
                renderDetailError(state.detailError);
                syncLayout();
                Dashboard.alert({ title: 'Browser Error', message: 'Failed to load themes: ' + err });
            });
        }

        function scheduleUiRefresh(options) {
            options = options || {};
            state.refreshFinderRequested = state.refreshFinderRequested || options.finder === true;
            state.refreshMappingsRequested = state.refreshMappingsRequested || options.mappings === true;
            if (state.uiRefreshInFlight) {
                state.uiRefreshQueued = true;
                return;
            }

            if (state.uiRefreshTimer) return;
            state.uiRefreshTimer = setTimeout(runUiRefresh, 0);
        }

        function runUiRefresh() {
            state.uiRefreshTimer = null;
            if (state.uiRefreshInFlight) {
                state.uiRefreshQueued = true;
                return;
            }

            state.uiRefreshInFlight = true;
            var refreshFinder = state.refreshFinderRequested || state.activeTab === 'finder';
            var refreshMappings = state.refreshMappingsRequested || state.activeTab === 'manage';
            state.refreshFinderRequested = false;
            state.refreshMappingsRequested = false;

            var selectedSeasonId = state.selectedSeason ? seasonRowId(state.selectedSeason) : null;
            var detailItemId = itemSelect.value || (state.currentItem ? value(state.currentItem, 'Id', 'id') : null);
            var refreshDetail = !!detailItemId && (state.detailLoading || state.detailError || !!state.currentResult);
            var tasks = [loadItems(false, { silent: true, preserveCount: true })];
            if (refreshDetail) {
                var detailToken = ++state.detailToken;
                tasks.push(loadThemes(detailToken, { silent: true, itemId: detailItemId }));
            }
            if (refreshFinder) {
                tasks.push(reloadSeasonMappingsPreservingState(selectedSeasonId));
            }
            if (refreshMappings) {
                state.mappingExplorerRows = [];
                tasks.push(loadAllSeasonMappings());
            }

            function completeRefresh() {
                state.uiRefreshInFlight = false;
                if (state.uiRefreshQueued) {
                    state.uiRefreshQueued = false;
                    scheduleUiRefresh();
                }
            }

            Promise.all(tasks).then(completeRefresh, function (err) {
                console.error('Failed to synchronize Browser state', err);
                completeRefresh();
            });
        }

        function renderHero() {
            var item = state.currentItem || {};
            var result = state.currentResult || {};
            var group = activeGroup() || {};
            title.textContent = text(value(group, 'Name', 'name') || value(result, 'Name', 'name'));
            var name = value(group, 'Name', 'name') || value(result, 'Name', 'name') || value(item, 'Name', 'name') || 'AT';
            posterFallback.textContent = String(name).trim().slice(0, 2).toUpperCase();
            setImage(poster, value(group, 'PrimaryImageUrl', 'primaryImageUrl') || value(item, 'PrimaryImageUrl', 'primaryImageUrl'), posterFallback);
            setImage(logo, value(item, 'LogoImageUrl', 'logoImageUrl'), null);
            setBackdrop(
                value(group, 'BackdropImageUrl', 'backdropImageUrl') ||
                value(group, 'ThumbImageUrl', 'thumbImageUrl') ||
                value(item, 'BackdropImageUrl', 'backdropImageUrl') ||
                value(item, 'ThumbImageUrl', 'thumbImageUrl'));

            meta.innerHTML = '';
            var count = activeRows().length;
            meta.className = 'ats-hero-meta';
            addHeroMetaChip(count + ' themes');
            var status = value(group, 'Status', 'status');
            if (status) {
                addHeroMetaChip(status);
            }
            var url = value(group, 'AnimeThemesUrl', 'animeThemesUrl') || value(result, 'AnimeThemesUrl', 'animeThemesUrl');
            if (url) {
                var link = document.createElement('a');
                link.className = 'ats-hero-meta-link';
                link.target = '_blank';
                link.rel = 'noopener';
                link.href = url;
                link.textContent = 'Open AnimeThemes';
                meta.appendChild(link);
            }
            syncMatchInFinderButton();
        }

        function addHeroMetaChip(label) {
            var chip = document.createElement('span');
            chip.className = 'ats-hero-meta-chip';
            chip.textContent = label;
            meta.appendChild(chip);
        }

        function renderSeasonGroups() {
            var groups = getGroups();
            seasonGroups.innerHTML = '';
            seasonGroups.style.display = groups.length > 1 ? '' : 'none';
            if (groups.length <= 1) {
                return;
            }

            groups.forEach(function (group) {
                var button = document.createElement('button');
                button.type = 'button';
                button.className = 'ats-season-pill';
                button.classList.toggle('active', groupId(group) === state.activeGroupId);
                var type = value(group, 'Type', 'type');
                var seasonNumber = value(group, 'SeasonNumber', 'seasonNumber');
                var label = type === 'Series' ? 'Series' : type === 'Season' && seasonNumber ? 'Season ' + seasonNumber : text(value(group, 'Name', 'name'));
                appendDiv(button, 'ats-season-pill-title', label);
                appendDiv(button, 'ats-season-pill-meta', [
                    value(group, 'Status', 'status'),
                    value(group, 'AnimeName', 'animeName') || value(group, 'AnimeThemesSlug', 'animeThemesSlug')
                ].filter(Boolean).join(' | '));
                button.addEventListener('click', function () {
                    state.activeGroupId = groupId(group);
                    renderSeasonGroups();
                    renderHero();
                    renderThemes();
                });
                seasonGroups.appendChild(button);
            });
        }

        function rowMatches(row) {
            var query = themeSearchInput.value.trim().toLowerCase();
            if (query) {
                var haystack = [
                    value(row, 'ThemeKey', 'themeKey'),
                    value(row, 'SongTitle', 'songTitle'),
                    value(row, 'Artists', 'artists'),
                    value(row, 'Episodes', 'episodes'),
                    value(row, 'Labels', 'labels')
                ].join(' ').toLowerCase();
                if (haystack.indexOf(query) === -1) return false;
            }

            var type = typeFilter.value;
            if (type !== 'all' && String(value(row, 'Type', 'type')).toUpperCase() !== type) return false;

            var saved = !!(value(row, 'BackdropExists', 'backdropExists') || value(row, 'ThemeMusicExists', 'themeMusicExists') || value(row, 'ExtraExists', 'extraExists'));
            if (statusFilter.value === 'saved' && !saved) return false;
            if (statusFilter.value === 'missing' && saved) return false;

            var spoiler = !!value(row, 'Spoiler', 'spoiler');
            var nsfw = !!value(row, 'Nsfw', 'nsfw');
            if (flagFilter.value === 'spoiler' && !spoiler) return false;
            if (flagFilter.value === 'nsfw' && !nsfw) return false;
            if (flagFilter.value === 'safe' && (spoiler || nsfw)) return false;
            return true;
        }

        function renderThemes() {
            if (state.detailLoading) {
                renderDetailLoading();
                return;
            }

            if (state.detailError) {
                renderDetailError(state.detailError);
                return;
            }

            var group = activeGroup() || {};
            var allRows = activeRows();
            var rows = allRows.filter(rowMatches);
            rowsContainer.innerHTML = '';
            if (!rows.length) {
                appendEmptyState(rowsContainer, 'No themes to show', value(group, 'EmptyMessage', 'emptyMessage') || 'Adjust filters or choose another season.');
                if (!allRows.length) {
                    appendEmptyMatchAction(rowsContainer, group);
                }
                return;
            }

            rows.forEach(function (row) {
                rowsContainer.appendChild(createThemeCard(row));
            });
            updateThemeCardDownloadStatuses();
        }

        function createThemeCard(row) {
            var card = document.createElement('div');
            card.className = 'ats-theme-card ats-fade-in';
            card.setAttribute('data-item-id', activeGroupItemId() || '');
            card.setAttribute('data-row-id', value(row, 'RowId', 'rowId') || '');
            card.setAttribute('data-theme-key', value(row, 'ThemeKey', 'themeKey'));

            var main = document.createElement('div');
            main.className = 'ats-theme-main';
            appendDiv(main, 'ats-theme-key', padOrder(value(row, 'Order', 'order')) + ' - ' + text(value(row, 'ThemeKey', 'themeKey')));
            appendDiv(main, 'ats-song', text(value(row, 'SongTitle', 'songTitle')));
            appendDiv(main, 'ats-artist fieldDescription', text(value(row, 'Artists', 'artists')));
            card.appendChild(main);

            var detail = document.createElement('div');
            detail.className = 'ats-detail';
            appendDiv(detail, 'fieldDescription', 'Episodes: ' + text(value(row, 'Episodes', 'episodes')));
            appendDiv(detail, 'fieldDescription', 'Quality: ' + text(value(row, 'Quality', 'quality')));
            var flags = document.createElement('div');
            flags.className = 'ats-status-list';
            addFlagChips(flags, row);
            detail.appendChild(flags);
            card.appendChild(detail);

            var side = document.createElement('div');
            side.className = 'ats-theme-side';
            var status = document.createElement('div');
            status.className = 'ats-status-list';
            addStatus(status, 'Video', value(row, 'BackdropExists', 'backdropExists'), value(row, 'BackdropPath', 'backdropPath'));
            addStatus(status, 'Audio', value(row, 'ThemeMusicExists', 'themeMusicExists'), value(row, 'ThemeMusicPath', 'themeMusicPath'));
            addStatus(status, 'Extras', value(row, 'ExtraExists', 'extraExists'), value(row, 'ExtraPath', 'extraPath'));
            side.appendChild(status);

        var actions = document.createElement('div');
        actions.className = 'ats-actions';

        var hasVideoUrl = !!value(row, 'VideoUrl', 'videoUrl');
        var hasVideoLocal = !!value(row, 'SavedVideoPlayable', 'savedVideoPlayable') || !!value(row, 'SavedExtraPlayable', 'savedExtraPlayable');
        if (hasVideoUrl || hasVideoLocal) {
            var videoStack = document.createElement('div');
            videoStack.className = 'ats-btn-stack';
            if (hasVideoUrl) {
                addRemotePreviewButton(videoStack, row, 'video', true);
            }
            addLocalVideoButton(videoStack, row);
            actions.appendChild(videoStack);
        }

        var hasAudioUrl = !!value(row, 'AudioUrl', 'audioUrl');
        var hasAudioLocal = !!value(row, 'SavedAudioPlayable', 'savedAudioPlayable');
        if (hasAudioUrl || hasAudioLocal) {
            var audioStack = document.createElement('div');
            audioStack.className = 'ats-btn-stack';
            if (hasAudioUrl) {
                addRemotePreviewButton(audioStack, row, 'audio', true);
            }
            addPlayButton(audioStack, row, 'audio', 'Play Audio', hasAudioLocal);
            actions.appendChild(audioStack);
        }

        var rightActions = document.createElement('div');
        rightActions.className = 'ats-theme-actions-right';

        var hasAnySaved = !!(value(row, 'BackdropExists', 'backdropExists') || value(row, 'ThemeMusicExists', 'themeMusicExists') || value(row, 'ExtraExists', 'extraExists'));
        if (hasAnySaved) {
            addDeleteButton(rightActions, row, true);
        }

        addDownloadButton(rightActions, row, true);
        actions.appendChild(rightActions);

        side.appendChild(actions);
        card.appendChild(side);
        var deletingTargets = deletingTargetsForRow(row);
        if (deletingTargets.length) {
            card.setAttribute('aria-busy', 'true');
            var deletingStatus = document.createElement('div');
            deletingStatus.className = 'ats-card-delete-status';
            deletingStatus.setAttribute('role', 'status');
            deletingStatus.textContent = 'Deleting ' + deletingTargets.join(', ') + '...';
            card.appendChild(deletingStatus);
        }
        return card;
    }

        function appendDiv(parent, className, content) {
            var div = document.createElement('div');
            div.className = className;
            div.textContent = content;
            parent.appendChild(div);
        }

        function padOrder(order) {
            var number = parseInt(order || 0, 10);
            return number > 0 && number < 10 ? '0' + number : String(order || '-');
        }

        function addFlagChips(container, row) {
            if (value(row, 'Spoiler', 'spoiler')) addChip(container, 'Spoiler', '');
            if (value(row, 'Nsfw', 'nsfw')) addChip(container, 'NSFW', '');
            var labels = value(row, 'Labels', 'labels');
            if (labels) {
                String(labels).split(',').forEach(function (label) { addChip(container, label.trim(), ''); });
            }
        }

        function addStatus(container, label, exists, path) {
            var chip = addChip(container, label + ': ' + (!path ? 'not selected' : exists ? 'saved' : 'missing'), exists ? 'ok' : 'missing');
            if (path) chip.title = path;
        }

        function addChip(container, label, modifier) {
            var span = document.createElement('span');
            span.className = 'ats-chip' + (modifier ? ' ' + modifier : '');
            span.textContent = label;
            container.appendChild(span);
            return span;
        }

        function embyIconCode(tone) {
            if (tone === 'download') return '\uE2C4';
            if (tone === 'play') return '\uE037';
            if (tone === 'link') return '\uE89E';
            if (tone === 'danger') return '\uE872';
            return '\uE913';
        }

        function createButton(label, secondary, tone) {
            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.type = 'button';
            button.className = secondary ? 'emby-button ats-button-secondary' : 'raised emby-button ats-action-button';
            if (tone) {
                button.className += ' ats-button-' + tone;
            }
            button.className += ' ats-icon-button-text';
            var icon = document.createElement('i');
            icon.className = 'md-icon ats-icon';
            icon.setAttribute('aria-hidden', 'true');
            icon.textContent = embyIconCode(tone);
            button.appendChild(icon);
            var labelSpan = document.createElement('span');
            labelSpan.textContent = label;
            button.appendChild(labelSpan);
            return button;
        }

        function addDownloadButton(container, row, iconOnly) {
            var button = createButton('Download', false, 'download');
            button.classList.add('ats-btn-download');
            if (iconOnly) {
                button.classList.add('ats-icon-button-only');
                button.classList.add('ats-button-align-right');
                button.title = 'Download theme';
            }
            button.addEventListener('click', function () {
                downloadTheme(row);
            });
            container.appendChild(button);
            return button;
        }

        function addLocalVideoButton(container, row) {
            var hasExtra = !!value(row, 'SavedExtraPlayable', 'savedExtraPlayable');
            var hasVideo = !!value(row, 'SavedVideoPlayable', 'savedVideoPlayable');
            var button = createButton('Play Video', true, 'play');
            button.classList.add('ats-btn-play-main');
            button.disabled = !hasExtra && !hasVideo;
            button.title = hasExtra ? 'Plays the browseable extras file.' : hasVideo ? 'Plays the local theme video file.' : 'No local video has been saved.';
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, hasExtra ? 'extra' : 'video');
            });
            container.appendChild(button);
        }

        function addPlayButton(container, row, target, label, playable) {
            var button = createButton(label, true, 'play');
            button.classList.add('ats-btn-play-main');
            button.disabled = !playable;
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, target);
            });
            container.appendChild(button);
        }

        function addOpenButton(container, label, url, iconOnly) {
            if (!url) return;
            var button = createButton(label, true, 'link');
            if (iconOnly) {
                button.classList.add('ats-icon-button-only');
                button.title = 'Open on AnimeThemes';
            }
            button.addEventListener('click', function () {
                window.open(url, '_blank', 'noopener');
            });
            container.appendChild(button);
        }

        function dispatchInput(input) {
            var event = document.createEvent('Event');
            event.initEvent('input', true, true);
            input.dispatchEvent(event);
        }

        function setupClearButtons() {
            page.querySelectorAll('.ats-clear-input').forEach(function (button) {
                var input = page.querySelector('#' + button.getAttribute('data-clear-target'));
                if (!input) return;
                var sync = function () {
                    button.style.display = input.value ? 'grid' : 'none';
                };
                button.addEventListener('click', function () {
                    input.value = '';
                    sync();
                    dispatchInput(input);
                    input.focus();
                });
                input.addEventListener('input', sync);
                sync();
            });
        }

        function syncSeasonFilterButtons() {
            var current = seasonFilter.value || 'unmatched';
            page.querySelectorAll('[data-season-filter]').forEach(function (button) {
                button.classList.toggle('active', button.getAttribute('data-season-filter') === current);
            });
        }

        function getConfigValue(config, key, defaultValue) {
            if (!config) return defaultValue;
            var camel = key.charAt(0).toLowerCase() + key.slice(1);
            var value = config[key] !== undefined ? config[key] : config[camel];
            return value === undefined || value === null ? defaultValue : value;
        }

        function ensureThemeConfig(config) {
            config = config || {};
            return {
                UseAsTheme: !!getConfigValue(config, 'UseAsTheme', true),
                MaxThemes: Math.max(0, parseInt(getConfigValue(config, 'MaxThemes', 1), 10) || 0),
                Volume: Math.max(0, Math.min(100, parseInt(getConfigValue(config, 'Volume', 100), 10) || 0)),
                IgnoreOp: !!getConfigValue(config, 'IgnoreOp', false),
                IgnoreEd: !!getConfigValue(config, 'IgnoreEd', true),
                IgnoreOverlaps: !!getConfigValue(config, 'IgnoreOverlaps', false),
                IgnoreCredits: !!getConfigValue(config, 'IgnoreCredits', false)
            };
        }

        function ensureMediaConfig(config) {
            config = config || {};
            return {
                Audio: ensureThemeConfig(getConfigValue(config, 'Audio', null)),
                Video: ensureThemeConfig(getConfigValue(config, 'Video', null))
            };
        }

        function ensureSettingsConfig(config) {
            config = config || {};
            config.ConfigurationVersion = 4;
            config.Series = ensureMediaConfig(getConfigValue(config, 'Series', null));
            config.Movie = ensureMediaConfig(getConfigValue(config, 'Movie', null));
            if (!Array.isArray(getConfigValue(config, 'SeasonThemeMappings', []))) {
                config.SeasonThemeMappings = [];
            }
            return config;
        }

        function normalizeSeasonThemeMappings(config) {
            var mappings = getConfigValue(config || {}, 'SeasonThemeMappings', []);
            return Array.isArray(mappings) ? mappings : [];
        }

        function cloneSettings(config) {
            return JSON.parse(JSON.stringify(config || {}));
        }

        function defaultThemeConfig() {
            return {
                UseAsTheme: true,
                MaxThemes: 1,
                Volume: 100,
                IgnoreOp: false,
                IgnoreEd: true,
                IgnoreOverlaps: false,
                IgnoreCredits: false
            };
        }

        function defaultMediaConfig() {
            return {
                Audio: defaultThemeConfig(),
                Video: defaultThemeConfig()
            };
        }

        function defaultSettingsConfig(existingConfig) {
            return ensureSettingsConfig({
                ConfigurationVersion: 4,
                ThemeDownloadingEnabled: true,
                MaxConcurrentDownloads: 1,
                DownloadTimeoutSeconds: 600,
                SegmentedDownloadEnabled: true,
                SegmentedDownloadSegments: 4,
                AllowAdd: true,
                ForceRedownload: false,
                AllowDelete: false,
                SeasonThemeDownloadsEnabled: true,
                ExtrasEnabled: false,
                ExtrasLinkMode: 0,
                ExtrasFileSuffix: 1,
                ExtrasFileNameFormat: '{Order}. {Theme} - {Song}',
                TagsEnabled: true,
                TagFormat: '{Season} {Year}',
                TagSeasonSpring: 'Spring',
                TagSeasonSummer: 'Summer',
                TagSeasonFall: 'Fall',
                TagSeasonWinter: 'Winter',
                Series: defaultMediaConfig(),
                Movie: defaultMediaConfig()
            });
        }

        function canonicalizeSettings(config) {
            config = ensureSettingsConfig(cloneSettings(config || {}));
            return {
                ConfigurationVersion: 4,
                ThemeDownloadingEnabled: !!getConfigValue(config, 'ThemeDownloadingEnabled', true),
                MaxConcurrentDownloads: Math.max(1, parseInt(getConfigValue(config, 'MaxConcurrentDownloads', 1), 10) || 1),
                DownloadTimeoutSeconds: Math.max(1, parseInt(getConfigValue(config, 'DownloadTimeoutSeconds', 600), 10) || 600),
                SegmentedDownloadEnabled: !!getConfigValue(config, 'SegmentedDownloadEnabled', true),
                SegmentedDownloadSegments: Math.max(2, Math.min(8, parseInt(getConfigValue(config, 'SegmentedDownloadSegments', 4), 10) || 4)),
                AllowAdd: !!getConfigValue(config, 'AllowAdd', true),
                ForceRedownload: !!getConfigValue(config, 'ForceRedownload', false),
                AllowDelete: !!getConfigValue(config, 'AllowDelete', false),
                SeasonThemeDownloadsEnabled: !!getConfigValue(config, 'SeasonThemeDownloadsEnabled', true),
                ExtrasEnabled: !!getConfigValue(config, 'ExtrasEnabled', false),
                ExtrasLinkMode: parseInt(normalizeExtrasLinkMode(getConfigValue(config, 'ExtrasLinkMode', 0)), 10) || 0,
                ExtrasFileSuffix: parseInt(normalizeExtrasFileSuffix(getConfigValue(config, 'ExtrasFileSuffix', 1)), 10),
                ExtrasFileNameFormat: String(getConfigValue(config, 'ExtrasFileNameFormat', '{Order}. {Theme} - {Song}')),
                TagsEnabled: !!getConfigValue(config, 'TagsEnabled', true),
                TagFormat: String(getConfigValue(config, 'TagFormat', '{Season} {Year}')),
                TagSeasonSpring: String(getConfigValue(config, 'TagSeasonSpring', 'Spring')),
                TagSeasonSummer: String(getConfigValue(config, 'TagSeasonSummer', 'Summer')),
                TagSeasonFall: String(getConfigValue(config, 'TagSeasonFall', 'Fall')),
                TagSeasonWinter: String(getConfigValue(config, 'TagSeasonWinter', 'Winter')),
                Series: ensureMediaConfig(getConfigValue(config, 'Series', null)),
                Movie: ensureMediaConfig(getConfigValue(config, 'Movie', null))
            };
        }

        function canonicalizeFullSettings(config) {
            var full = canonicalizeSettings(config);
            delete full.SeasonThemeMappings;
            return full;
        }

        function serializeSettings(config) {
            return JSON.stringify(canonicalizeSettings(config));
        }

        function serializeFullSettings(config) {
            return JSON.stringify(canonicalizeFullSettings(config), null, 2);
        }

        function settingsSnapshotFromForm() {
            return serializeSettings(readSettingsForm());
        }

        function settingsDirty() {
            return state.settingsLoaded && settingsSnapshotFromForm() !== state.settingsSnapshot;
        }

        function syncSettingsDirty() {
            if (state.settingsApplying || !settingsFields.SaveButton) return;
            var dirty = settingsDirty();
            settingsFields.SaveButton.disabled = !dirty;
            if (settingsFields.ResetDefaultsButton) {
                settingsFields.ResetDefaultsButton.disabled = !state.settingsLoaded;
            }
            if (state.settingsLoaded) {
                setSettingsState(dirty ? 'Unsaved changes.' : 'No unsaved changes.');
            }
        }

        function captureSettingsSnapshot() {
            state.settingsSnapshot = settingsSnapshotFromForm();
            syncSettingsDirty();
        }

        function normalizeExtrasLinkMode(value) {
            if (value === 'HardLinkOnly') return '1';
            if (value === 'CopyOnly') return '2';
            if (value === 'HardLinkWithCopyFallback') return '0';
            return String(value || 0);
        }

        function normalizeExtrasFileSuffix(value) {
            if (value === 'None') return '0';
            if (value === 'Other') return '1';
            if (value === 'Short') return '2';
            if (value === 'Scene') return '3';
            return String(value === undefined || value === null ? 1 : value);
        }

        function setSettingsState(message) {
            settingsState.textContent = message || 'Ready.';
        }

        function profileId(profile) {
            return profile === 'movie' ? 'Movie' : 'Series';
        }

        function mediaId(media) {
            return media === 'video' ? 'Video' : 'Audio';
        }

        function profileFieldId(profile, media, field) {
            return 'Ats' + profileId(profile) + mediaId(media) + field;
        }

        function profileFields(profile, media) {
            return {
                UseAsTheme: page.querySelector('#' + profileFieldId(profile, media, 'UseAsTheme')),
                MaxThemes: page.querySelector('#' + profileFieldId(profile, media, 'MaxThemes')),
                VolumeSlider: page.querySelector('#' + profileFieldId(profile, media, 'VolumeSlider')),
                Volume: page.querySelector('#' + profileFieldId(profile, media, 'Volume')),
                Mute: page.querySelector('#' + profileFieldId(profile, media, 'Mute')),
                IgnoreOp: page.querySelector('#' + profileFieldId(profile, media, 'IgnoreOp')),
                IgnoreEd: page.querySelector('#' + profileFieldId(profile, media, 'IgnoreEd')),
                IgnoreOverlaps: page.querySelector('#' + profileFieldId(profile, media, 'IgnoreOverlaps')),
                IgnoreCredits: page.querySelector('#' + profileFieldId(profile, media, 'IgnoreCredits'))
            };
        }

        function clampVolume(value) {
            value = parseInt(value, 10);
            if (isNaN(value)) value = 0;
            return Math.max(0, Math.min(100, value));
        }

        function setVolumeFields(fields, value) {
            var volume = clampVolume(value);
            if (!fields.Volume || !fields.VolumeSlider || !fields.Mute) return;
            fields.Volume.value = volume;
            fields.VolumeSlider.value = volume;
            fields.Mute.checked = volume === 0;
            if (volume > 0) {
                fields.Volume.dataset.lastNonZero = String(volume);
            } else if (!fields.Volume.dataset.lastNonZero) {
                fields.Volume.dataset.lastNonZero = '100';
            }
        }

        function bindVolumeControl(profile, media) {
            var fields = profileFields(profile, media);
            if (!fields.Volume || !fields.VolumeSlider || !fields.Mute) return;

            fields.VolumeSlider.addEventListener('input', function () {
                setVolumeFields(fields, fields.VolumeSlider.value);
            });
            fields.Volume.addEventListener('input', function () {
                setVolumeFields(fields, fields.Volume.value);
            });
            fields.Mute.addEventListener('change', function () {
                if (fields.Mute.checked) {
                    setVolumeFields(fields, 0);
                    return;
                }

                setVolumeFields(fields, fields.Volume.dataset.lastNonZero || 100);
            });
        }

        function profileCardHtml(profile, media) {
            var profileName = profileId(profile);
            var mediaName = mediaId(media);
            var title = mediaName === 'Audio' ? 'Audio / theme-music' : 'Video / backdrops';
            var volumeHelp = mediaName === 'Audio' ? '100 = original, 1-99 = re-encode, 0 = mute.' : '100 = original, 0 = remove audio.';
            return [
                '<div class="ats-profile-card">',
                '<h5>' + title + '</h5>',
                '<div class="checkboxContainer checkboxContainer-withDescription">',
                '<label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'UseAsTheme" type="checkbox" is="emby-checkbox" /><span>Use as server theme</span></label>',
                '<div class="fieldDescription">Save selected media in ' + (mediaName === 'Audio' ? 'theme-music' : 'backdrops') + '. Disable video here while keeping Extras enabled for Extra-only output.</div>',
                '</div>',
                '<div class="inputContainer">',
                '<label class="inputLabel inputLabelUnfocused" for="Ats' + profileName + mediaName + 'MaxThemes">Max themes</label>',
                '<input id="Ats' + profileName + mediaName + 'MaxThemes" type="number" is="emby-input" min="0" />',
                '<div class="fieldDescription">Maximum number of themes to download for this output. 0 disables this output.</div>',
                '</div>',
                '<div class="inputContainer">',
                '<label class="inputLabel inputLabelUnfocused" for="Ats' + profileName + mediaName + 'Volume">Volume</label>',
                '<div class="ats-volume-row">',
                '<input id="Ats' + profileName + mediaName + 'VolumeSlider" class="ats-volume-range" type="range" min="0" max="100" step="1" />',
                '<input id="Ats' + profileName + mediaName + 'Volume" class="ats-volume-number" type="number" is="emby-input" min="0" max="100" />',
                '<label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'Mute" type="checkbox" is="emby-checkbox" /><span>Mute</span></label>',
                '</div>',
                '<div class="fieldDescription">' + volumeHelp + '</div>',
                '</div>',
                '<div class="ats-toggle-list">',
                '<div><label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'IgnoreOp" type="checkbox" is="emby-checkbox" /><span>Ignore OP</span></label><div class="fieldDescription">Skip opening themes for this output.</div></div>',
                '<div><label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'IgnoreEd" type="checkbox" is="emby-checkbox" /><span>Ignore ED</span></label><div class="fieldDescription">Skip ending themes for this output.</div></div>',
                '<div><label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'IgnoreOverlaps" type="checkbox" is="emby-checkbox" /><span>Ignore overlaps</span></label><div class="fieldDescription">Skip themes marked as overlapping with episode content.</div></div>',
                '<div><label class="emby-checkbox-label"><input id="Ats' + profileName + mediaName + 'IgnoreCredits" type="checkbox" is="emby-checkbox" /><span>Ignore credits</span></label><div class="fieldDescription">Skip creditless or credits variants when AnimeThemes marks them separately.</div></div>',
                '</div>',
                '</div>'
            ].join('');
        }

        function buildProfileControls() {
            ['series', 'movie'].forEach(function (profile) {
                var container = page.querySelector('.ats-profile-grid[data-settings-profile="' + profile + '"]');
                if (!container || container.getAttribute('data-built') === 'true') return;
                container.innerHTML = profileCardHtml(profile, 'audio') + profileCardHtml(profile, 'video');
                container.setAttribute('data-built', 'true');
                bindVolumeControl(profile, 'audio');
                bindVolumeControl(profile, 'video');
            });
        }

        function renderThemeSettings(profile, media, theme) {
            var fields = profileFields(profile, media);
            theme = ensureThemeConfig(theme);
            fields.UseAsTheme.checked = theme.UseAsTheme;
            fields.MaxThemes.value = theme.MaxThemes;
            setVolumeFields(fields, theme.Volume);
            fields.IgnoreOp.checked = theme.IgnoreOp;
            fields.IgnoreEd.checked = theme.IgnoreEd;
            fields.IgnoreOverlaps.checked = theme.IgnoreOverlaps;
            fields.IgnoreCredits.checked = theme.IgnoreCredits;
        }

        function collectThemeSettings(profile, media) {
            var fields = profileFields(profile, media);
            return ensureThemeConfig({
                UseAsTheme: fields.UseAsTheme.checked,
                MaxThemes: parseInt(fields.MaxThemes.value, 10) || 0,
                Volume: clampVolume(fields.Volume.value),
                IgnoreOp: fields.IgnoreOp.checked,
                IgnoreEd: fields.IgnoreEd.checked,
                IgnoreOverlaps: fields.IgnoreOverlaps.checked,
                IgnoreCredits: fields.IgnoreCredits.checked
            });
        }

        function renderAllProfileSettings() {
            ensureSettingsConfig(state.settingsConfig);
            renderThemeSettings('series', 'audio', state.settingsConfig.Series.Audio);
            renderThemeSettings('series', 'video', state.settingsConfig.Series.Video);
            renderThemeSettings('movie', 'audio', state.settingsConfig.Movie.Audio);
            renderThemeSettings('movie', 'video', state.settingsConfig.Movie.Video);
        }

        function applyAllProfileSettings() {
            if (!state.settingsConfig) return;
            ensureSettingsConfig(state.settingsConfig);
            state.settingsConfig.Series.Audio = collectThemeSettings('series', 'audio');
            state.settingsConfig.Series.Video = collectThemeSettings('series', 'video');
            state.settingsConfig.Movie.Audio = collectThemeSettings('movie', 'audio');
            state.settingsConfig.Movie.Video = collectThemeSettings('movie', 'video');
        }

        function syncConditionalSettings() {
            if (settingsFields.SegmentedDownloadOptions) {
                settingsFields.SegmentedDownloadOptions.hidden = !settingsFields.SegmentedDownloadEnabled.checked;
            }
            if (settingsFields.ExtrasOptions) {
                settingsFields.ExtrasOptions.hidden = !settingsFields.ExtrasEnabled.checked;
            }
            if (settingsFields.TagOptions) {
                settingsFields.TagOptions.hidden = !settingsFields.TagsEnabled.checked;
            }
            updateFormatPreviews();
        }

        function replaceFormatTokens(format, tokens) {
            return String(format || '').replace(/\{([A-Za-z0-9]+)\}/g, function (match, key) {
                return tokens[key.toLowerCase()] !== undefined ? tokens[key.toLowerCase()] : '';
            }).replace(/(\s*-\s*){2,}/g, ' - ').replace(/^\s*-\s*|\s*-\s*$/g, '').trim();
        }

        function updateFormatPreviews() {
            if (settingsFields.ExtrasFormatPreview && settingsFields.ExtrasFileNameFormat) {
                var extrasFormat = settingsFields.ExtrasFileNameFormat.value || '{Order}. {Theme} - {Song}';
                var extrasPreview = replaceFormatTokens(extrasFormat, {
                    order: '01',
                    theme: 'OP1',
                    type: 'OP',
                    sequence: '1',
                    version: '',
                    song: 'Sample Song',
                    artist: 'Sample Artist',
                    episodes: 'Eps 1-12',
                    labels: 'BD NC',
                    quality: '1080'
                }) || 'OP1';
                var suffixValue = normalizeExtrasFileSuffix(settingsFields.ExtrasFileSuffix.value);
                var suffix = suffixValue === '2' ? '-short' : suffixValue === '3' ? '-scene' : suffixValue === '0' ? '' : '-other';
                settingsFields.ExtrasFormatPreview.textContent = extrasPreview.replace(/[\\/:*?"<>|]/g, ' ').trim() + suffix + '.webm';
            }

            if (settingsFields.TagFormatPreview && settingsFields.TagFormat) {
                var seasonName = settingsFields.TagSeasonWinter && settingsFields.TagSeasonWinter.value ? settingsFields.TagSeasonWinter.value : 'Winter';
                var tagPreview = replaceFormatTokens(settingsFields.TagFormat.value || '{Season} {Year}', {
                    season: seasonName,
                    year: '2024'
                }) || seasonName + ' 2024';
                settingsFields.TagFormatPreview.textContent = tagPreview;
            }
        }

        function showCopyState(message) {
            if (!settingsFields.CopyCssMessage) return;
            settingsFields.CopyCssMessage.textContent = message;
            settingsFields.CopyCssMessage.style.display = '';
            window.setTimeout(function () {
                settingsFields.CopyCssMessage.style.display = 'none';
            }, 1800);
        }

        function copyCustomCss() {
            var textarea = settingsFields.CustomCssText;
            if (!textarea) return;
            var css = textarea.value || textarea.textContent || '';
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(css).then(function () {
                    showCopyState('Copied.');
                }).catch(function () {
                    textarea.select();
                    document.execCommand('copy');
                    showCopyState('Copied.');
                });
                return;
            }

            textarea.select();
            document.execCommand('copy');
            showCopyState('Copied.');
        }

        function applySettingsToForm(config, captureSnapshot) {
            state.settingsApplying = true;
            state.settingsConfig = ensureSettingsConfig(config);
            settingsFields.ThemeDownloadingEnabled.checked = !!getConfigValue(config, 'ThemeDownloadingEnabled', true);
            settingsFields.MaxConcurrentDownloads.value = getConfigValue(config, 'MaxConcurrentDownloads', 1);
            settingsFields.DownloadTimeoutSeconds.value = getConfigValue(config, 'DownloadTimeoutSeconds', 600);
            settingsFields.SegmentedDownloadEnabled.checked = !!getConfigValue(config, 'SegmentedDownloadEnabled', true);
            settingsFields.SegmentedDownloadSegments.value = Math.max(2, Math.min(8, parseInt(getConfigValue(config, 'SegmentedDownloadSegments', 4), 10) || 4));
            settingsFields.AllowAdd.checked = !!getConfigValue(config, 'AllowAdd', true);
            settingsFields.ForceRedownload.checked = !!getConfigValue(config, 'ForceRedownload', false);
            settingsFields.AllowDelete.checked = !!getConfigValue(config, 'AllowDelete', false);
            settingsFields.SeasonThemeDownloadsEnabled.checked = !!getConfigValue(config, 'SeasonThemeDownloadsEnabled', true);
            settingsFields.ExtrasEnabled.checked = !!getConfigValue(config, 'ExtrasEnabled', false);
            settingsFields.ExtrasLinkMode.value = normalizeExtrasLinkMode(getConfigValue(config, 'ExtrasLinkMode', 0));
            settingsFields.ExtrasFileSuffix.value = normalizeExtrasFileSuffix(getConfigValue(config, 'ExtrasFileSuffix', 1));
            settingsFields.ExtrasFileNameFormat.value = getConfigValue(config, 'ExtrasFileNameFormat', '{Order}. {Theme} - {Song}');
            settingsFields.TagsEnabled.checked = !!getConfigValue(config, 'TagsEnabled', true);
            settingsFields.TagFormat.value = getConfigValue(config, 'TagFormat', '{Season} {Year}');
            settingsFields.TagSeasonSpring.value = getConfigValue(config, 'TagSeasonSpring', 'Spring');
            settingsFields.TagSeasonSummer.value = getConfigValue(config, 'TagSeasonSummer', 'Summer');
            settingsFields.TagSeasonFall.value = getConfigValue(config, 'TagSeasonFall', 'Fall');
            settingsFields.TagSeasonWinter.value = getConfigValue(config, 'TagSeasonWinter', 'Winter');
            syncConditionalSettings();
            renderAllProfileSettings();
            state.settingsApplying = false;
            if (captureSnapshot === false) {
                syncSettingsDirty();
            } else {
                captureSettingsSnapshot();
            }
        }

        function removeLegacySettings(config) {
            [
                'SeriesAudioMaxThemes', 'SeriesAudioVolume', 'SeriesAudioIgnoreOp', 'SeriesAudioIgnoreEd', 'SeriesAudioIgnoreOverlaps', 'SeriesAudioIgnoreCredits',
                'SeriesVideoMaxThemes', 'SeriesVideoVolume', 'SeriesVideoIgnoreOp', 'SeriesVideoIgnoreEd', 'SeriesVideoIgnoreOverlaps', 'SeriesVideoIgnoreCredits',
                'MovieAudioMaxThemes', 'MovieAudioVolume', 'MovieAudioIgnoreOp', 'MovieAudioIgnoreEd', 'MovieAudioIgnoreOverlaps', 'MovieAudioIgnoreCredits',
                'MovieVideoMaxThemes', 'MovieVideoVolume', 'MovieVideoIgnoreOp', 'MovieVideoIgnoreEd', 'MovieVideoIgnoreOverlaps', 'MovieVideoIgnoreCredits'
            ].forEach(function (key) {
                delete config[key];
                delete config[key.charAt(0).toLowerCase() + key.slice(1)];
            });
        }

        function readSettingsForm() {
            return {
                ConfigurationVersion: 4,
                ThemeDownloadingEnabled: settingsFields.ThemeDownloadingEnabled.checked,
                MaxConcurrentDownloads: parseInt(settingsFields.MaxConcurrentDownloads.value, 10) || 1,
                DownloadTimeoutSeconds: parseInt(settingsFields.DownloadTimeoutSeconds.value, 10) || 600,
                SegmentedDownloadEnabled: settingsFields.SegmentedDownloadEnabled.checked,
                SegmentedDownloadSegments: Math.max(2, Math.min(8, parseInt(settingsFields.SegmentedDownloadSegments.value, 10) || 4)),
                AllowAdd: settingsFields.AllowAdd.checked,
                ForceRedownload: settingsFields.ForceRedownload.checked,
                AllowDelete: settingsFields.AllowDelete.checked,
                SeasonThemeDownloadsEnabled: settingsFields.SeasonThemeDownloadsEnabled.checked,
                ExtrasEnabled: settingsFields.ExtrasEnabled.checked,
                ExtrasLinkMode: parseInt(settingsFields.ExtrasLinkMode.value, 10) || 0,
                ExtrasFileSuffix: parseInt(settingsFields.ExtrasFileSuffix.value, 10),
                ExtrasFileNameFormat: settingsFields.ExtrasFileNameFormat.value,
                TagsEnabled: settingsFields.TagsEnabled.checked,
                TagFormat: settingsFields.TagFormat.value,
                TagSeasonSpring: settingsFields.TagSeasonSpring.value,
                TagSeasonSummer: settingsFields.TagSeasonSummer.value,
                TagSeasonFall: settingsFields.TagSeasonFall.value,
                TagSeasonWinter: settingsFields.TagSeasonWinter.value,
                Series: {
                    Audio: collectThemeSettings('series', 'audio'),
                    Video: collectThemeSettings('series', 'video')
                },
                Movie: {
                    Audio: collectThemeSettings('movie', 'audio'),
                    Video: collectThemeSettings('movie', 'video')
                }
            };
        }

        function collectSettingsFromForm(config) {
            var form = canonicalizeSettings(readSettingsForm());
            config = ensureSettingsConfig(config || state.settingsConfig);
            Object.keys(form).forEach(function (key) {
                config[key] = form[key];
            });
            removeLegacySettings(config);
            state.settingsConfig = ensureSettingsConfig(config);
            return state.settingsConfig;
        }

        function loadSettings(force) {
            if (state.settingsLoaded && !force) return;
            setSettingsState('Loading settings...');
            ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                state.settingsLoaded = true;
                applySettingsToForm(config || {});
                setSettingsState('Settings loaded.');
            }).catch(function (err) {
                setSettingsState('Failed to load settings.');
                Dashboard.alert({ title: 'Settings Error', message: getErrorMessage(err) });
            });
        }

        function saveSettings(showResult) {
            if (!settingsDirty()) {
                syncSettingsDirty();
                return Promise.resolve();
            }
            setSettingsState('Saving settings...');
            return ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                config = collectSettingsFromForm(config || {});
                return ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(function (result) {
                    state.settingsLoaded = true;
                    captureSettingsSnapshot();
                    setSettingsState('Settings saved.');
                    if (showResult) {
                        Dashboard.processPluginConfigurationUpdateResult(result);
                    }
                    return result;
                });
            }).catch(function (err) {
                setSettingsState('Failed to save settings.');
                Dashboard.alert({ title: 'Settings Error', message: getErrorMessage(err) });
                throw err;
            });
        }

        function runScheduledTask() {
            var saveIfNeeded = settingsDirty() ? saveSettings(false) : Promise.resolve();
            saveIfNeeded.then(function () {
                return ApiClient.getScheduledTasks();
            }).then(function (tasks) {
                var task = tasks.find(function (t) { return t.Key === 'AnimeThemesSyncDownloader'; });
                if (!task) {
                    throw new Error('AnimeThemes scheduled task was not found.');
                }
                return ApiClient.startScheduledTask(task.Id);
            }).then(function () {
                setSettingsState('Scheduled task started.');
                Dashboard.alert('Task started.');
            }).catch(function (err) {
                setSettingsState('Failed to start task.');
                Dashboard.alert({ title: 'Task Error', message: getErrorMessage(err) });
            });
        }

        function resetSettingsDefaults() {
            var savedSnapshot = state.settingsSnapshot;
            applySettingsToForm(defaultSettingsConfig(state.settingsConfig || {}), false);
            state.settingsSnapshot = savedSnapshot;
            syncSettingsDirty();
        }

        function downloadTheme(row) {
            var itemId = activeGroupItemId();
            var rowId = value(row, 'RowId', 'rowId');
            if (!itemId || !rowId) return;

            var settingsPromise = state.settingsConfig
                ? Promise.resolve(ensureSettingsConfig(state.settingsConfig))
                : ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                    state.settingsConfig = ensureSettingsConfig(config || {});
                    return state.settingsConfig;
                });

            settingsPromise.then(function (config) {
                var groupType = String(value(activeGroup(), 'Type', 'type') || value(state.currentResult, 'Type', 'type') || 'Series');
                var profile = groupType === 'Movie' ? config.Movie : config.Series;
                var audioDefault = profile.Audio.UseAsTheme && profile.Audio.MaxThemes > 0;
                var videoDefault = profile.Video.UseAsTheme && profile.Video.MaxThemes > 0;
                var extrasDefault = !!getConfigValue(config, 'ExtrasEnabled', false) && profile.Video.MaxThemes > 0;
                openDownloadDialog(row, itemId, rowId, videoDefault, audioDefault, extrasDefault);
            }).catch(function (err) {
                Dashboard.alert({ title: 'Settings Error', message: 'Failed to load download defaults: ' + getErrorMessage(err) });
            });
        }

        function syncDownloadOptionStyles() {
            [downloadIncludeVideo, downloadIncludeAudio, downloadIncludeExtras].forEach(function (control) {
                if (control) {
                    var option = control.closest('.ats-download-option');
                    if (option) option.classList.toggle('selected', control.checked);
                }
            });
        }

        function openDownloadDialog(row, itemId, rowId, includeVideo, includeAudio, includeExtras) {
            state.lastFocus = document.activeElement;
            state.pendingDownload = { itemId: itemId, rowId: rowId };
            downloadDialogTheme.textContent = text(value(row, 'ThemeKey', 'themeKey')) + (value(row, 'SongTitle', 'songTitle') ? ' · ' + value(row, 'SongTitle', 'songTitle') : '');
            downloadIncludeVideo.checked = includeVideo;
            downloadIncludeAudio.checked = includeAudio;
            downloadIncludeExtras.checked = includeExtras;
            downloadDialogError.hidden = true;
            syncDownloadOptionStyles();
            downloadDialog.classList.add('open');
            downloadDialog.setAttribute('aria-hidden', 'false');
            downloadDialogConfirm.focus();
        }

        function closeDownloadDialog() {
            if (downloadDialog.contains(document.activeElement)) {
                document.activeElement.blur();
            }

            downloadDialog.classList.remove('open');
            downloadDialog.setAttribute('aria-hidden', 'true');
            downloadDialogError.hidden = true;
            state.pendingDownload = null;
            if (state.lastFocus && typeof state.lastFocus.focus === 'function') {
                state.lastFocus.focus();
            }
        }

        function confirmDownloadSelection() {
            var pending = state.pendingDownload;
            if (!pending) return;
            if (!downloadIncludeVideo.checked && !downloadIncludeAudio.checked && !downloadIncludeExtras.checked) {
                downloadDialogError.hidden = false;
                return;
            }

            var path = 'AnimeThemesSync/Jobs/ThemeDownload?ItemId=' + encodeURIComponent(pending.itemId) +
                '&RowId=' + encodeURIComponent(pending.rowId) +
                '&Force=false&IncludeAudio=' + encodeURIComponent(downloadIncludeAudio.checked) +
                '&IncludeVideo=' + encodeURIComponent(downloadIncludeVideo.checked) +
                '&IncludeExtras=' + encodeURIComponent(downloadIncludeExtras.checked) +
                '&DisplayTitle=' + encodeURIComponent(downloadDialogTheme.textContent || pending.rowId);
            var request = {
                path: path,
                jobType: 'Theme',
                itemId: pending.itemId,
                rowId: pending.rowId,
                title: downloadDialogTheme.textContent || pending.rowId
            };
            closeDownloadDialog();
            startDownloadJob(request);
        }

        function downloadItem() {
            var itemId = activeGroupItemId();
            if (!itemId) return;
            var displayTitle = title.textContent || 'Item download';
            startDownloadJob({
                path: 'AnimeThemesSync/Jobs/ItemDownload?ItemId=' + encodeURIComponent(itemId) +
                    '&Force=false&DisplayTitle=' + encodeURIComponent(displayTitle),
                jobType: 'Item',
                itemId: itemId,
                rowId: '',
                title: displayTitle
            });
        }

        function startDownloadJob(request) {
            var temporaryId = 'starting-' + (++state.downloadSequence);
            var optimisticJob = {
                jobId: temporaryId,
                clientOnly: true,
                status: 'Starting',
                progress: 0,
                message: 'Starting...',
                error: '',
                jobType: request.jobType,
                itemId: request.itemId || '',
                rowId: request.rowId || '',
                title: request.title || 'Download job',
                queuePosition: null,
                canCancel: false
            };
            state.activeDownloads.unshift(optimisticJob);
            renderDownloadManager();
            updateThemeCardDownloadStatuses();
            if (downloadManager) {
                downloadManager.style.display = 'flex';
                downloadManager.classList.remove('collapsed');
                downloadManager.removeAttribute('aria-hidden');
            }

            apiPost(request.path).then(function (job) {
                state.activeDownloads = state.activeDownloads.filter(function (current) { return current.jobId !== temporaryId; });
                if (value(job, 'JobId', 'jobId')) {
                    var startedJob = normalizeDownloadJob(job);
                    startedJob.clientOnly = true;
                    state.activeDownloads.unshift(startedJob);
                }
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                startDownloadsPolling();
            }).catch(function (err) {
                state.activeDownloads = state.activeDownloads.filter(function (current) { return current.jobId !== temporaryId; });
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                Dashboard.alert({ title: 'Download Error', message: 'Failed to start download: ' + getErrorMessage(err) });
            });
        }

        function startDownloadBatch(request) {
            var temporaryId = 'starting-' + (++state.downloadSequence);
            state.activeDownloads.unshift({
                jobId: temporaryId,
                clientOnly: true,
                status: 'Starting',
                progress: 0,
                message: 'Planning downloads...',
                error: '',
                jobType: 'Item',
                itemId: request.itemId || '',
                rowId: '',
                title: request.title || 'Season download',
                queuePosition: null,
                canCancel: false,
                canRetry: false
            });
            renderDownloadManager();
            if (downloadManager) {
                downloadManager.style.display = 'flex';
                downloadManager.classList.remove('collapsed');
                downloadManager.removeAttribute('aria-hidden');
            }
            apiPost(request.path).then(function (jobs) {
                state.activeDownloads = state.activeDownloads.filter(function (current) { return current.jobId !== temporaryId; });
                (jobs || []).map(normalizeDownloadJob).reverse().forEach(function (job) { state.activeDownloads.unshift(job); });
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                startDownloadsPolling();
                if (!jobs || !jobs.length) finderState.textContent = 'Mapping saved. No downloads were needed.';
            }).catch(function (err) {
                state.activeDownloads = state.activeDownloads.filter(function (current) { return current.jobId !== temporaryId; });
                renderDownloadManager();
                Dashboard.alert({ title: 'Download Error', message: 'Failed to plan downloads: ' + getErrorMessage(err) });
            });
        }

        function startDownloadsPolling() {
            state.downloadsPollingEnabled = true;
            if (state.downloadsInterval) return;
            if (state.downloadsPollInFlight) {
                state.downloadsPollRequested = true;
                return;
            }
            pollDownloads();
        }

        function stopDownloadsPolling() {
            state.downloadsPollingEnabled = false;
            state.downloadsPollRequested = false;
            if (state.downloadsInterval) {
                clearTimeout(state.downloadsInterval);
                state.downloadsInterval = null;
            }
        }

        function scheduleDownloadsPolling(delay) {
            if (!state.downloadsPollingEnabled) return;
            if (state.downloadsInterval) clearTimeout(state.downloadsInterval);
            state.downloadsInterval = setTimeout(function () {
                state.downloadsInterval = null;
                pollDownloads();
            }, typeof delay === 'number' ? delay : 750);
        }

        function pollDownloadsNow() {
            state.downloadsPollingEnabled = true;
            if (state.downloadsInterval) clearTimeout(state.downloadsInterval);
            state.downloadsInterval = null;
            if (state.downloadsPollInFlight) state.downloadsPollRequested = true;
            else pollDownloads();
        }

        function isActiveDownloadStatus(status) {
            return status === 'Starting' || status === 'Running' || status === 'Pending' || status === 'Cancelling';
        }

        function isTerminalDownloadStatus(status) {
            return status === 'Completed' || status === 'Failed' || status === 'Cancelled';
        }

        function normalizeDownloadJob(job) {
            return {
                jobId: value(job, 'JobId', 'jobId') || '',
                status: value(job, 'Status', 'status') || 'Pending',
                progress: Math.max(0, Math.min(100, Number(value(job, 'Progress', 'progress')) || 0)),
                message: value(job, 'Message', 'message') || '',
                error: value(job, 'Error', 'error') || '',
                jobType: value(job, 'JobType', 'jobType') || 'Download',
                itemId: value(job, 'ItemId', 'itemId') || '',
                rowId: value(job, 'RowId', 'rowId') || '',
                title: value(job, 'DisplayTitle', 'displayTitle') || 'Download job',
                queuePosition: value(job, 'QueuePosition', 'queuePosition'),
                canCancel: !!value(job, 'CanCancel', 'canCancel'),
                canRetry: !!value(job, 'CanRetry', 'canRetry')
            };
        }

        function pollDownloads() {
            if (state.downloadsPollInFlight) return;
            state.downloadsPollInFlight = true;
            var shouldContinue = false;
            apiGet('AnimeThemesSync/Jobs').then(function (jobs) {
                var list = jobs || [];
                var serverJobs = list.map(normalizeDownloadJob);
                var previousStatuses = {};
                Object.keys(state.downloadStatuses).forEach(function (jobId) {
                    previousStatuses[jobId] = state.downloadStatuses[jobId];
                });
                state.activeDownloads.forEach(function (job) {
                    if (job.jobId && String(job.jobId).indexOf('starting-') !== 0) previousStatuses[job.jobId] = job.status;
                });
                var serverJobIds = {};
                serverJobs.forEach(function (job) { serverJobIds[job.jobId] = true; });
                var optimisticJobs = state.activeDownloads.filter(function (job) { return job.clientOnly && !serverJobIds[job.jobId]; });
                state.activeDownloads = optimisticJobs.concat(serverJobs);
                var terminalTransition = false;
                var nextStatuses = {};
                serverJobs.forEach(function (job) {
                    nextStatuses[job.jobId] = job.status;
                    if (state.downloadStatusesInitialized && isActiveDownloadStatus(previousStatuses[job.jobId]) && isTerminalDownloadStatus(job.status)) {
                        terminalTransition = true;
                    }
                });
                state.downloadStatuses = nextStatuses;
                state.downloadStatusesInitialized = true;
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                if (terminalTransition) scheduleUiRefresh();
                shouldContinue = state.activeDownloads.some(function (job) { return isActiveDownloadStatus(job.status); });
            }).catch(function (err) {
                console.error('Failed to poll downloads', err);
                shouldContinue = state.activeDownloads.some(function (job) { return isActiveDownloadStatus(job.status); });
            }).then(function () {
                state.downloadsPollInFlight = false;
                if (!state.downloadsPollingEnabled) return;
                if (state.downloadsPollRequested) {
                    state.downloadsPollRequested = false;
                    scheduleDownloadsPolling(0);
                } else if (shouldContinue) scheduleDownloadsPolling(750);
                else stopDownloadsPolling();
            });
        }

        function cancelDownloadJob(jobId) {
            apiPost('AnimeThemesSync/Jobs/' + encodeURIComponent(jobId) + '/Cancel').then(function (job) {
                var updated = normalizeDownloadJob(job || {});
                state.activeDownloads = state.activeDownloads.map(function (current) {
                    return current.jobId === jobId && updated.jobId ? updated : current;
                });
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                pollDownloadsNow();
            }).catch(function (err) {
                console.error('Failed to cancel job', err);
            });
        }

        function dismissDownloadJob(jobId) {
            var previousJobs = state.activeDownloads.slice();
            state.activeDownloads = state.activeDownloads.filter(function (job) { return job.jobId !== jobId; });
            renderDownloadManager();
            updateThemeCardDownloadStatuses();
            apiDeleteNoContent('AnimeThemesSync/Jobs/' + encodeURIComponent(jobId)).catch(function (err) {
                state.activeDownloads = previousJobs;
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                pollDownloadsNow();
                Dashboard.alert({ title: 'Download History Error', message: 'Failed to remove download history: ' + getErrorMessage(err) });
            });
        }

        function retryDownloadJob(jobId) {
            apiPost('AnimeThemesSync/Jobs/' + encodeURIComponent(jobId) + '/Retry').then(function (job) {
                var updated = normalizeDownloadJob(job || {});
                state.activeDownloads = state.activeDownloads.map(function (current) {
                    return current.jobId === jobId && updated.jobId ? updated : current;
                });
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                startDownloadsPolling();
            }).catch(function (err) {
                Dashboard.alert({ title: 'Download Retry Error', message: getErrorMessage(err) });
            });
        }

        function clearFinishedDownloadHistory() {
            var previousJobs = state.activeDownloads.slice();
            state.activeDownloads = state.activeDownloads.filter(function (job) { return job.status !== 'Completed' && job.status !== 'Cancelled'; });
            renderDownloadManager();
            updateThemeCardDownloadStatuses();
            apiDeleteNoContent('AnimeThemesSync/Jobs/History').catch(function (err) {
                state.activeDownloads = previousJobs;
                renderDownloadManager();
                updateThemeCardDownloadStatuses();
                Dashboard.alert({ title: 'Download History Error', message: getErrorMessage(err) });
            });
        }

        function toggleDownloadManager() {
            if (downloadManager) {
                downloadManager.classList.toggle('collapsed');
                var expanded = !downloadManager.classList.contains('collapsed');
                if (dmToggle) {
                    dmToggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
                    dmToggle.setAttribute('aria-label', expanded ? 'Collapse downloads' : 'Expand downloads');
                }
            }
        }

        function downloadStatusText(job) {
            if (job.status === 'Starting') return 'Starting...';
            if (job.status === 'Pending') return 'Queued' + (job.queuePosition ? ' #' + job.queuePosition : '');
            if (job.status === 'Running') return Math.round(job.progress) + '% · ' + (job.message || 'Downloading...');
            if (job.status === 'Cancelling') return 'Cancelling...';
            if (job.status === 'Failed') return 'Failed: ' + (job.error || 'Unknown error');
            if (job.status === 'Cancelled') return 'Cancelled';
            return 'Completed';
        }

        function downloadJobKey(job) {
            return String(job.jobId || (job.jobType + ':' + job.itemId + ':' + job.rowId));
        }

        function updateProgressTrack(track, bar, job, animateFromZero) {
            var progress = Math.max(0, Math.min(100, Number(job.progress) || 0));
            var visualProgress = isActiveDownloadStatus(job.status) && progress === 0 ? 1 : progress;
            var indeterminate = job.status === 'Starting' || job.status === 'Pending';
            track.classList.toggle('ats-progress-indeterminate', indeterminate);
            track.setAttribute('aria-label', job.title);
            track.setAttribute('aria-valuemin', '0');
            track.setAttribute('aria-valuemax', '100');
            track.setAttribute('aria-valuetext', downloadStatusText(job));
            if (indeterminate) track.removeAttribute('aria-valuenow');
            else track.setAttribute('aria-valuenow', String(Math.round(progress)));

            if (indeterminate) {
                bar.style.width = '36%';
                return;
            }

            function applyWidth() { bar.style.width = visualProgress + '%'; }
            if (animateFromZero) {
                bar.style.width = '0%';
                window.requestAnimationFrame(applyWidth);
            } else {
                applyWidth();
            }
        }

        function createDownloadManagerItem(key) {
            var item = document.createElement('div');
            item.setAttribute('data-job-key', key);

            var header = document.createElement('div');
            header.className = 'ats-dm-item-header';
            var titleSpan = document.createElement('div');
            titleSpan.className = 'ats-dm-item-title';
            header.appendChild(titleSpan);
            var headerActions = document.createElement('div');
            headerActions.className = 'ats-dm-item-actions';
            var cancelBtn = document.createElement('button');
            cancelBtn.type = 'button';
            cancelBtn.className = 'emby-button ats-dm-item-cancel';
            cancelBtn.textContent = 'Cancel';
            cancelBtn.addEventListener('click', function (event) {
                event.stopPropagation();
                var jobId = item.getAttribute('data-job-id');
                if (jobId) cancelDownloadJob(jobId);
            });
            headerActions.appendChild(cancelBtn);
            var retryBtn = document.createElement('button');
            retryBtn.type = 'button';
            retryBtn.className = 'emby-button ats-dm-item-retry';
            retryBtn.textContent = 'Retry';
            retryBtn.addEventListener('click', function (event) {
                event.stopPropagation();
                var jobId = item.getAttribute('data-job-id');
                if (jobId) retryDownloadJob(jobId);
            });
            headerActions.appendChild(retryBtn);
            var dismissBtn = document.createElement('button');
            dismissBtn.type = 'button';
            dismissBtn.className = 'emby-button ats-dm-item-dismiss';
            dismissBtn.addEventListener('click', function (event) {
                event.stopPropagation();
                var jobId = item.getAttribute('data-job-id');
                if (jobId) dismissDownloadJob(jobId);
            });
            headerActions.appendChild(dismissBtn);
            header.appendChild(headerActions);
            item.appendChild(header);

            var statusRow = document.createElement('div');
            statusRow.className = 'ats-download-status-row';
            var dot = document.createElement('span');
            dot.className = 'ats-download-state-dot';
            dot.setAttribute('aria-hidden', 'true');
            statusRow.appendChild(dot);
            var progressText = document.createElement('div');
            progressText.className = 'ats-dm-item-progress-text';
            statusRow.appendChild(progressText);
            item.appendChild(statusRow);

            var progressTrack = document.createElement('div');
            progressTrack.className = 'ats-dm-item-progress-track';
            progressTrack.setAttribute('role', 'progressbar');
            var progressBar = document.createElement('div');
            progressBar.className = 'ats-dm-item-progress-bar';
            progressTrack.appendChild(progressBar);
            item.appendChild(progressTrack);
            return item;
        }

        function updateDownloadManagerItem(item, job, isNew) {
            item.className = 'ats-dm-item ' + String(job.status || '').toLowerCase();
            item.setAttribute('data-job-id', job.clientOnly ? '' : job.jobId);
            var titleSpan = item.querySelector('.ats-dm-item-title');
            titleSpan.textContent = job.title;
            titleSpan.title = job.title;
            var cancelBtn = item.querySelector('.ats-dm-item-cancel');
            cancelBtn.hidden = !job.canCancel;
            cancelBtn.disabled = !job.canCancel;
            cancelBtn.setAttribute('aria-label', 'Cancel ' + job.title);
            var retryBtn = item.querySelector('.ats-dm-item-retry');
            retryBtn.hidden = job.clientOnly || !job.canRetry;
            retryBtn.disabled = job.clientOnly || !job.canRetry;
            retryBtn.setAttribute('aria-label', 'Retry ' + job.title);
            var dismissBtn = item.querySelector('.ats-dm-item-dismiss');
            dismissBtn.hidden = job.clientOnly || !isTerminalDownloadStatus(job.status);
            dismissBtn.disabled = job.clientOnly || !isTerminalDownloadStatus(job.status);
            dismissBtn.setAttribute('aria-label', 'Remove ' + job.title + ' from download history');
            var progressText = item.querySelector('.ats-dm-item-progress-text');
            progressText.textContent = downloadStatusText(job);
            progressText.classList.toggle('ats-color-danger', job.status === 'Failed');
            item.querySelector('.ats-download-state-dot').hidden = !isActiveDownloadStatus(job.status);
            updateProgressTrack(item.querySelector('.ats-dm-item-progress-track'), item.querySelector('.ats-dm-item-progress-bar'), job, isNew);
        }

        function renderDownloadManager() {
            if (!downloadManager) return;
            var activeJobs = state.activeDownloads.filter(function (job) { return isActiveDownloadStatus(job.status); });
            if (dmBadge) dmBadge.textContent = activeJobs.length;
            if (dmClearHistory) {
                var hasClearableHistory = state.activeDownloads.some(function (job) { return job.status === 'Completed' || job.status === 'Cancelled'; });
                dmClearHistory.hidden = !hasClearableHistory;
                dmClearHistory.disabled = !hasClearableHistory;
            }

            if (state.activeDownloads.length === 0) {
                downloadManager.style.display = 'flex';
                downloadManager.removeAttribute('aria-hidden');
                if (dmContent) dmContent.innerHTML = '<div class="ats-dm-empty">No active downloads</div>';
                return;
            }

            downloadManager.style.display = 'flex';
            downloadManager.removeAttribute('aria-hidden');
            if (!dmContent) return;
            var emptyState = dmContent.querySelector('.ats-dm-empty');
            if (emptyState) emptyState.remove();

            var retained = {};
            state.activeDownloads.forEach(function (job) {
                var key = downloadJobKey(job);
                var item = dmContent.querySelector('[data-job-key="' + key.replace(/"/g, '\\"') + '"]');
                var isNew = !item;
                if (!item) item = createDownloadManagerItem(key);
                updateDownloadManagerItem(item, job, isNew);
                dmContent.appendChild(item);
                retained[key] = true;
            });
            Array.prototype.slice.call(dmContent.querySelectorAll('[data-job-key]')).forEach(function (item) {
                if (!retained[item.getAttribute('data-job-key')]) item.remove();
            });
        }

        function renderInlineDownloadStatus(container, job) {
            var key = downloadJobKey(job);
            var isNew = container.getAttribute('data-job-key') !== key;
            container.hidden = false;
            container.setAttribute('aria-live', 'polite');
            container.setAttribute('data-job-key', key);
            container.setAttribute('data-job-id', job.clientOnly ? '' : job.jobId);
            if (isNew) {
                container.innerHTML = '';
                var dot = document.createElement('span');
                dot.className = 'ats-download-state-dot';
                dot.setAttribute('aria-hidden', 'true');
                container.appendChild(dot);
                var copy = document.createElement('span');
                copy.className = 'ats-card-download-copy';
                container.appendChild(copy);
                var track = document.createElement('span');
                track.className = 'ats-card-download-track';
                track.setAttribute('role', 'progressbar');
                var bar = document.createElement('span');
                bar.className = 'ats-card-download-bar';
                track.appendChild(bar);
                container.appendChild(track);
                var cancel = document.createElement('button');
                cancel.type = 'button';
                cancel.className = 'emby-button ats-card-download-cancel';
                cancel.textContent = 'Cancel';
                cancel.addEventListener('click', function () {
                    var jobId = container.getAttribute('data-job-id');
                    if (jobId) cancelDownloadJob(jobId);
                });
                container.appendChild(cancel);
                var retry = document.createElement('button');
                retry.type = 'button';
                retry.className = 'emby-button ats-card-download-retry';
                retry.textContent = 'Retry';
                retry.addEventListener('click', function () {
                    var jobId = container.getAttribute('data-job-id');
                    if (jobId) retryDownloadJob(jobId);
                });
                container.appendChild(retry);
            }

            container.querySelector('.ats-card-download-copy').textContent = downloadStatusText(job);
            var cancelButton = container.querySelector('.ats-card-download-cancel');
            cancelButton.hidden = !job.canCancel;
            cancelButton.disabled = !job.canCancel;
            cancelButton.setAttribute('aria-label', 'Cancel ' + job.title);
            var retryButton = container.querySelector('.ats-card-download-retry');
            retryButton.hidden = !job.canRetry;
            retryButton.disabled = !job.canRetry;
            retryButton.setAttribute('aria-label', 'Retry ' + job.title);
            updateProgressTrack(container.querySelector('.ats-card-download-track'), container.querySelector('.ats-card-download-bar'), job, isNew);
        }

        function updateThemeCardDownloadStatuses() {
            page.querySelectorAll('.ats-theme-card').forEach(function (card) {
                var downloadBtn = card.querySelector('.ats-btn-download');
                if (!downloadBtn) return;
                var itemId = card.getAttribute('data-item-id') || '';
                var rowId = card.getAttribute('data-row-id') || '';
                var matchingJobs = state.activeDownloads.filter(function (d) {
                    return d.jobType === 'Theme' &&
                        String(d.itemId).toLowerCase() === String(itemId).toLowerCase() && String(d.rowId) === String(rowId);
                });
                var activeJob = matchingJobs.find(function (job) { return isActiveDownloadStatus(job.status); });
                var failedJob = matchingJobs.find(function (job) { return job.status === 'Failed' && job.canRetry; });
                var visibleJob = activeJob || failedJob;

                if (visibleJob) {
                    downloadBtn.classList.toggle('ats-downloading', !!activeJob);
                    downloadBtn.disabled = !!activeJob;
                    downloadBtn.title = downloadStatusText(visibleJob);
                    var status = card.querySelector('.ats-card-download-status');
                    if (!status) {
                        status = document.createElement('div');
                        status.className = 'ats-card-download-status';
                        card.appendChild(status);
                    }
                    renderInlineDownloadStatus(status, visibleJob);
                } else {
                    downloadBtn.classList.remove('ats-downloading');
                    downloadBtn.disabled = false;
                    downloadBtn.title = 'Download theme';
                    var status = card.querySelector('.ats-card-download-status');
                    if (status) status.remove();
                }
            });

            var itemStatus = page.querySelector('#AnimeThemesItemDownloadStatus');
            var currentItemId = activeGroupItemId() || '';
            var itemJob = state.activeDownloads.find(function (job) {
                return isActiveDownloadStatus(job.status) && job.jobType === 'Item' &&
                    String(job.itemId).toLowerCase() === String(currentItemId).toLowerCase();
            });
            if (itemStatus && itemJob) {
                downloadItemButton.disabled = true;
                renderInlineDownloadStatus(itemStatus, itemJob);
            } else if (itemStatus) {
                itemStatus.hidden = true;
                itemStatus.innerHTML = '';
                if (!state.detailLoading) downloadItemButton.disabled = false;
            }
        }

        function addDeleteButton(container, row, iconOnly) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'emby-button ats-button-secondary' + (iconOnly ? ' ats-icon-button-only' : '');

            var icon = document.createElement('i');
            icon.className = 'md-icon ats-icon ats-color-danger';
            icon.innerHTML = '&#xE872;';
            button.appendChild(icon);

            if (!iconOnly) {
                var textSpan = document.createElement('span');
                textSpan.textContent = 'Delete';
                button.appendChild(textSpan);
            }

            button.title = 'Delete saved files';
            button.addEventListener('click', function () {
                openDeleteDialog(row);
            });
            container.appendChild(button);
        }

        function openDeleteDialog(row) {
            state.lastFocus = document.activeElement;
            state.pendingDelete = row;
            deleteDialogTheme.textContent = text(value(row, 'ThemeKey', 'themeKey')) + (value(row, 'SongTitle', 'songTitle') ? ' · ' + value(row, 'SongTitle', 'songTitle') : '');

            var hasVideo = !!value(row, 'BackdropExists', 'backdropExists');
            var hasAudio = !!value(row, 'ThemeMusicExists', 'themeMusicExists');
            var hasExtras = !!value(row, 'ExtraExists', 'extraExists');

            var optionVideo = page.querySelector('#AtsDeleteOptionVideo');
            var optionAudio = page.querySelector('#AtsDeleteOptionAudio');
            var optionExtras = page.querySelector('#AtsDeleteOptionExtras');

            if (optionVideo) optionVideo.style.display = hasVideo ? '' : 'none';
            if (optionAudio) optionAudio.style.display = hasAudio ? '' : 'none';
            if (optionExtras) optionExtras.style.display = hasExtras ? '' : 'none';

            deleteIncludeVideo.checked = hasVideo;
            deleteIncludeAudio.checked = hasAudio;
            deleteIncludeExtras.checked = hasExtras;
            deleteDialogError.hidden = true;

            syncDeleteOptionStyles();
            deleteDialog.classList.add('open');
            deleteDialog.setAttribute('aria-hidden', 'false');
            deleteDialogConfirm.focus();
        }

        function closeDeleteDialog() {
            if (deleteDialog.contains(document.activeElement)) {
                document.activeElement.blur();
            }
            deleteDialog.classList.remove('open');
            deleteDialog.setAttribute('aria-hidden', 'true');
            deleteDialogError.hidden = true;
            state.pendingDelete = null;
            if (state.lastFocus && typeof state.lastFocus.focus === 'function') {
                state.lastFocus.focus();
            }
        }

        function syncDeleteOptionStyles() {
            [deleteIncludeVideo, deleteIncludeAudio, deleteIncludeExtras].forEach(function (control) {
                if (control) {
                    var option = control.closest('.ats-download-option');
                    if (option) option.classList.toggle('selected', control.checked);
                }
            });
        }

        function confirmDeleteSelection() {
            var row = state.pendingDelete;
            if (!row) return;
            if (!deleteIncludeVideo.checked && !deleteIncludeAudio.checked && !deleteIncludeExtras.checked) {
                deleteDialogError.hidden = false;
                return;
            }

            var targets = [];
            if (deleteIncludeVideo.checked) targets.push('video');
            if (deleteIncludeAudio.checked) targets.push('audio');
            if (deleteIncludeExtras.checked) targets.push('extra');
            closeDeleteDialog();
            deleteRowTargets(row, targets);
        }

        function themeTargetKey(row, target, itemId) {
            return String(itemId || activeGroupItemId() || '').toLowerCase() + ':' + String(value(row, 'RowId', 'rowId') || '') + ':' + target;
        }

        function deletingTargetsForRow(row) {
            return ['video', 'audio', 'extra'].filter(function (target) {
                return !!state.deletingThemeTargets[themeTargetKey(row, target)];
            });
        }

        function markThemeTargetDeleted(row, target) {
            if (target === 'video') {
                row.BackdropExists = row.backdropExists = false;
                row.SavedVideoPlayable = row.savedVideoPlayable = false;
                row.BackdropPath = row.backdropPath = null;
            } else if (target === 'audio') {
                row.ThemeMusicExists = row.themeMusicExists = false;
                row.SavedAudioPlayable = row.savedAudioPlayable = false;
                row.ThemeMusicPath = row.themeMusicPath = null;
            } else {
                row.ExtraExists = row.extraExists = false;
                row.SavedExtraPlayable = row.savedExtraPlayable = false;
                row.ExtraPath = row.extraPath = null;
            }
        }

        function snapshotThemeTarget(row, target) {
            if (target === 'video') {
                return {
                    exists: !!value(row, 'BackdropExists', 'backdropExists'),
                    playable: !!value(row, 'SavedVideoPlayable', 'savedVideoPlayable'),
                    path: value(row, 'BackdropPath', 'backdropPath')
                };
            }
            if (target === 'audio') {
                return {
                    exists: !!value(row, 'ThemeMusicExists', 'themeMusicExists'),
                    playable: !!value(row, 'SavedAudioPlayable', 'savedAudioPlayable'),
                    path: value(row, 'ThemeMusicPath', 'themeMusicPath')
                };
            }
            return {
                exists: !!value(row, 'ExtraExists', 'extraExists'),
                playable: !!value(row, 'SavedExtraPlayable', 'savedExtraPlayable'),
                path: value(row, 'ExtraPath', 'extraPath')
            };
        }

        function restoreThemeTarget(row, target, snapshot) {
            if (target === 'video') {
                row.BackdropExists = row.backdropExists = snapshot.exists;
                row.SavedVideoPlayable = row.savedVideoPlayable = snapshot.playable;
                row.BackdropPath = row.backdropPath = snapshot.path;
            } else if (target === 'audio') {
                row.ThemeMusicExists = row.themeMusicExists = snapshot.exists;
                row.SavedAudioPlayable = row.savedAudioPlayable = snapshot.playable;
                row.ThemeMusicPath = row.themeMusicPath = snapshot.path;
            } else {
                row.ExtraExists = row.extraExists = snapshot.exists;
                row.SavedExtraPlayable = row.savedExtraPlayable = snapshot.playable;
                row.ExtraPath = row.extraPath = snapshot.path;
            }
        }

        function deleteRowTargets(row, targets) {
            var itemId = activeGroupItemId();
            var rowId = value(row, 'RowId', 'rowId');
            if (!itemId || !rowId || !targets.length) return;

            if (player.classList.contains('open')) {
                closePlayer();
            } else {
                state.playerLoadToken++;
                releasePlayerMedia();
            }

            var snapshots = {};
            var targetKeys = targets.map(function (target) { return themeTargetKey(row, target, itemId); });
            targets.forEach(function (target, index) {
                snapshots[target] = snapshotThemeTarget(row, target);
                state.deletingThemeTargets[targetKeys[index]] = true;
                markThemeTargetDeleted(row, target);
            });
            renderThemes();
            setProgress(true, 'Deleting selected theme files...', 0);

            var requests = targets.map(function (target) {
                var path = 'AnimeThemesSync/ThemeFiles/DeleteFile?ItemId=' + encodeURIComponent(itemId) +
                    '&RowId=' + encodeURIComponent(rowId) + '&Target=' + encodeURIComponent(target);
                return apiPost(path).then(function (result) {
                    return { ok: true, result: result, target: target };
                }, function (error) {
                    return { ok: false, error: error, target: target };
                });
            });

            Promise.all(requests).then(function (outcomes) {
                targetKeys.forEach(function (key) { delete state.deletingThemeTargets[key]; });
                var files = 0;
                var bytes = 0;
                outcomes.forEach(function (outcome) {
                    if (!outcome.ok) {
                        restoreThemeTarget(row, outcome.target, snapshots[outcome.target]);
                        return;
                    }
                    files += Number(value(outcome.result, 'FilesDeleted', 'filesDeleted')) || 0;
                    bytes += Number(value(outcome.result, 'BytesDeleted', 'bytesDeleted')) || 0;
                });
                renderThemes();
                scheduleUiRefresh();
                var failure = outcomes.find(function (outcome) { return !outcome.ok; });
                if (failure) {
                    setProgress(true, 'Delete partially failed; refreshing...', 0);
                    Dashboard.alert({ title: 'Delete Error', message: 'Some files could not be deleted: ' + getErrorMessage(failure.error) });
                } else {
                    setProgress(true, 'Deleted ' + files + ' files (' + formatBytes(bytes) + ')', 100);
                    setTimeout(function () { setProgress(false, '', 0); }, 1800);
                }
            });
        }

        function deleteThemeFiles(scope) {
            var label = scope === 'all' ? 'all AnimeThemes files' : scope === 'audio' ? 'theme songs' : 'theme videos and extras';
            if (!window.confirm('Delete ' + label + '?')) {
                return;
            }

            if (player.classList.contains('open')) closePlayer();
            else {
                state.playerLoadToken++;
                releasePlayerMedia();
            }
            setProgress(true, 'Deleting ' + label + '...', 0);
            var bulkTargets = scope === 'all' ? ['video', 'audio', 'extra'] : scope === 'audio' ? ['audio'] : ['video', 'extra'];
            activeRows().forEach(function (row) { bulkTargets.forEach(function (target) { markThemeTargetDeleted(row, target); }); });
            renderThemes();
            apiPost('AnimeThemesSync/ThemeFiles/Delete?Scope=' + encodeURIComponent(scope)).then(function (result) {
                var files = value(result, 'FilesDeleted', 'filesDeleted') || 0;
                var bytes = value(result, 'BytesDeleted', 'bytesDeleted') || 0;
                setProgress(true, 'Deleted ' + files + ' files (' + formatBytes(bytes) + ')', 100);
                scheduleUiRefresh();
                setTimeout(function () { setProgress(false, '', 0); }, 1800);
            }).catch(function (err) {
                setProgress(true, 'Delete failed: ' + getErrorMessage(err), 0);
                scheduleUiRefresh();
                Dashboard.alert({ title: 'Delete Error', message: getErrorMessage(err) });
            });
        }

        function addDeleteFileButton(container, row, target, label, exists, iconOnly) {
            if (!exists) return;
            var button = createButton(label, true, 'danger');
            if (iconOnly) {
                button.classList.add('ats-icon-button-only');
                button.title = label;
            }
            button.addEventListener('click', function () {
                deleteIndividualThemeFile(row, target);
            });
            container.appendChild(button);
        }

        function deleteIndividualThemeFile(row, target) {
            var itemId = activeGroupItemId();
            var rowId = value(row, 'RowId', 'rowId');
            if (!itemId || !rowId) return;

            var label = target === 'audio' ? 'theme song' : target === 'video' ? 'theme video' : 'extras file';
            if (!window.confirm('Delete this local ' + label + '?')) {
                return;
            }

            deleteRowTargets(row, [target]);
        }

        function openPlayer(row, target) {
            var rowId = value(row, 'RowId', 'rowId');
            var itemId = activeGroupItemId();
            openMediaPlayer(row, target, false, function () {
                return apiUrl('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes/' + encodeURIComponent(rowId) + '/LocalMedia?target=' + encodeURIComponent(target) + '&_=' + Date.now(), true);
            });
        }

        function openRemotePlayer(row, target, src) {
            openMediaPlayer(row, target, true, function () { return src; });
        }

        function openMediaPlayer(row, target, isPreview, sourceFactory) {
            state.lastFocus = document.activeElement;
            playerTitle.textContent = text(value(row, 'ThemeKey', 'themeKey')) + (isPreview ? ' - preview ' : ' - ') + target;
            player.classList.add('open');
            player.setAttribute('aria-hidden', 'false');
            loadPlayerAttempt(target, isPreview, sourceFactory);
        }

        function loadPlayerAttempt(target, isPreview, sourceFactory) {
            state.playerLoadToken++;
            var token = state.playerLoadToken;
            if (state.playerLoadTimer) clearTimeout(state.playerLoadTimer);
            releasePlayerMedia();
            var wrapper = document.createElement('div');
            wrapper.className = 'ats-player-wrapper' + (target === 'audio' ? ' ats-player-wrapper-audio' : ' ats-player-wrapper-video');
            var loader = document.createElement('div');
            loader.className = 'ats-player-loader';
            loader.setAttribute('role', 'status');
            loader.setAttribute('aria-live', 'polite');
            var spinner = document.createElement('div');
            spinner.className = 'ats-spinner';
            spinner.setAttribute('aria-hidden', 'true');
            loader.appendChild(spinner);
            var loadingText = document.createElement('span');
            loadingText.textContent = isPreview ? 'Loading preview...' : 'Loading media...';
            loader.appendChild(loadingText);
            wrapper.appendChild(loader);
            var element = document.createElement(target === 'audio' ? 'audio' : 'video');
            element.controls = true;
            element.autoplay = true;
            element.preload = 'auto';
            element.style.width = '100%';
            element.style.height = target === 'audio' ? '54px' : '100%';
            var failedOnce = false;
            function ready() {
                if (failedOnce || token !== state.playerLoadToken) return;
                if (state.playerLoadTimer) clearTimeout(state.playerLoadTimer);
                state.playerLoadTimer = null;
                if (loader.parentNode) loader.remove();
            }
            function failed() {
                if (failedOnce || token !== state.playerLoadToken) return;
                failedOnce = true;
                if (state.playerLoadTimer) clearTimeout(state.playerLoadTimer);
                state.playerLoadTimer = null;
                if (!loader.parentNode) wrapper.appendChild(loader);
                loader.innerHTML = '';
                var message = document.createElement('span');
                message.className = 'ats-color-danger';
                message.textContent = isPreview ? 'Preview failed to load' : 'Failed to load media';
                loader.appendChild(message);
                var actions = document.createElement('div');
                actions.className = 'ats-player-error-actions';
                var retry = createButton('Retry', true);
                retry.addEventListener('click', function () { loadPlayerAttempt(target, isPreview, sourceFactory); });
                actions.appendChild(retry);
                var close = createButton('Close', true);
                close.addEventListener('click', closePlayer);
                actions.appendChild(close);
                loader.appendChild(actions);
            }
            element.addEventListener('canplay', ready);
            element.addEventListener('loadedmetadata', ready);
            element.addEventListener('error', failed);
            wrapper.appendChild(element);
            playerBody.appendChild(wrapper);
            state.playerLoadTimer = setTimeout(failed, 30000);
            element.src = sourceFactory();
            element.load();
        }

        function releasePlayerMedia() {
            Array.prototype.slice.call(playerBody.querySelectorAll('audio, video')).forEach(function (media) {
                try {
                    media.pause();
                    media.removeAttribute('src');
                    media.load();
                } catch (ignored) {
                    // The element may already have been detached.
                }
            });
            playerBody.innerHTML = '';
        }

        function closePlayer() {
            if (player.contains(document.activeElement)) {
                document.activeElement.blur();
            }

            state.playerLoadToken++;
            if (state.playerLoadTimer) clearTimeout(state.playerLoadTimer);
            state.playerLoadTimer = null;
            releasePlayerMedia();
            player.classList.remove('open');
            player.setAttribute('aria-hidden', 'true');
            if (state.lastFocus && typeof state.lastFocus.focus === 'function') {
                state.lastFocus.focus();
            }
        }

        page.querySelectorAll('.ats-tab').forEach(function (button) {
            button.addEventListener('click', function () {
                setActiveTab(button.getAttribute('data-ats-tab') || 'library');
            });
        });
        page.querySelector('#AnimeThemesSeasonRefresh').addEventListener('click', function () {
            reloadSeasonMappingsPreservingState();
        });
        seasonFilter.addEventListener('change', function () {
            syncSeasonFilterButtons();
            state.finderScrollTop = 0;
            loadSeasonMappings(false);
        });
        page.querySelectorAll('[data-season-filter]').forEach(function (button) {
            button.addEventListener('click', function () {
                seasonFilter.value = button.getAttribute('data-season-filter') || 'unmatched';
                syncSeasonFilterButtons();
                state.finderScrollTop = 0;
                loadSeasonMappings(false);
            });
        });
        seasonSearch.addEventListener('input', function () {
            if (state.finderListSearchTimer) clearTimeout(state.finderListSearchTimer);
            state.finderListSearchTimer = setTimeout(function () {
                state.finderScrollTop = 0;
                loadSeasonMappings(false);
            }, 250);
        });
        [seasonSort, seasonSortDirection].forEach(function (control) {
            control.addEventListener('change', function () {
                state.finderScrollTop = 0;
                loadSeasonMappings(false);
            });
        });
        seasonList.addEventListener('scroll', function () {
            state.finderScrollTop = seasonList.scrollTop;
        });
        page.querySelector('#AnimeThemesFinderSearch').addEventListener('click', function () {
            searchAnimeThemes(false);
        });
        finderSearchInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                searchAnimeThemes(false);
            }
        });
        finderSearchInput.addEventListener('input', function () {
            finderState.textContent = 'Ready. Use Search to refresh candidates.';
        });
        finderYear.addEventListener('input', function () {
            finderState.textContent = 'Ready. Use Search to refresh candidates.';
        });
        searchInput.addEventListener('input', function () {
            scheduleLoadItems();
        });
        [libraryTypeFilter, libraryLinkFilter, librarySavedFilter, librarySort, librarySortDirection].forEach(function (control) {
            if (control) {
                control.addEventListener('change', function () {
                    loadItems(false);
                });
            }
        });
        themeSearchInput.addEventListener('input', renderThemes);
        typeFilter.addEventListener('change', renderThemes);
        statusFilter.addEventListener('change', renderThemes);
        flagFilter.addEventListener('change', renderThemes);
        gridSizeSelect.addEventListener('change', function () {
            setViewSize(gridSizeSelect.value);
        });
        showDetailsCheckbox.addEventListener('change', function () {
            state.showLibraryDetails = showDetailsCheckbox.checked;
            renderItemGrid();
        });
        itemSelect.addEventListener('change', function () {
            if (itemSelect.value) openItemDetail(itemSelect.value);
        });
        page.querySelector('#AnimeThemesBrowserRefreshItems').addEventListener('click', function () {
            loadItems(false);
        });
        var rebuildButton = page.querySelector('#AnimeThemesBrowserRebuildCache');
        if (rebuildButton) {
            rebuildButton.addEventListener('click', function () {
                postMaintenance('AnimeThemesSync/BrowserCache/Rebuild', 'Browser Cache');
            });
        }

        var clearCacheButton = page.querySelector('#AnimeThemesBrowserClearCache');
        if (clearCacheButton) {
            clearCacheButton.addEventListener('click', function () {
                postMaintenance('AnimeThemesSync/BrowserCache/Clear', 'Browser Cache');
            });
        }

        var importLegacyButton = page.querySelector('#AnimeThemesImportLegacyManifests');
        if (importLegacyButton) {
            importLegacyButton.addEventListener('click', function () {
                postMaintenance('AnimeThemesSync/Extras/ImportLegacyManifests', 'Legacy Manifests');
            });
        }
        page.querySelector('#AnimeThemesBrowserDownload').addEventListener('click', downloadItem);
        matchInFinderButton.addEventListener('click', function () {
            openFinderForSeasonGroup(activeGroup());
        });
        page.querySelector('#AnimeThemesBrowserBack').addEventListener('click', openLibraryView);
        page.querySelectorAll('.ats-view-mode').forEach(function (button) {
            button.addEventListener('click', function () {
                setViewMode(button.getAttribute('data-view-mode') || 'poster');
            });
        });
        page.querySelectorAll('[data-delete-scope]').forEach(function (button) {
            button.addEventListener('click', function () {
                deleteThemeFiles(button.getAttribute('data-delete-scope') || 'all');
            });
        });
        buildProfileControls();
        settingsFields.SegmentedDownloadEnabled.addEventListener('change', syncConditionalSettings);
        settingsFields.ExtrasEnabled.addEventListener('change', syncConditionalSettings);
        settingsFields.TagsEnabled.addEventListener('change', syncConditionalSettings);
        [settingsFields.ExtrasFileNameFormat, settingsFields.ExtrasFileSuffix, settingsFields.TagFormat, settingsFields.TagSeasonWinter].forEach(function (input) {
            if (input) {
                input.addEventListener('input', updateFormatPreviews);
            }
        });
        syncConditionalSettings();
        page.querySelector('#AtsSettingsSave').addEventListener('click', function () {
            saveSettings(true);
        });
        if (settingsFields.ResetDefaultsButton) {
            settingsFields.ResetDefaultsButton.addEventListener('click', resetSettingsDefaults);
        }
        page.querySelector('#AtsRunTask').addEventListener('click', runScheduledTask);
        settingsView.addEventListener('input', syncSettingsDirty);
        settingsView.addEventListener('change', syncSettingsDirty);
        if (settingsFields.CopyCssButton) {
            settingsFields.CopyCssButton.addEventListener('click', copyCustomCss);
        }
        page.querySelector('#AtsExportConfig').addEventListener('click', exportConfig);
        page.querySelector('#AtsImportLoadCurrent').addEventListener('click', loadCurrentConfigForImport);
        importApplyButton.addEventListener('click', applyImportConfig);
        importJsonInput.addEventListener('input', function () {
            parseImportJson(false);
        });
        importFileInput.addEventListener('change', handleImportFile);
        page.querySelector('#AtsExportMappings').addEventListener('click', exportMappings);
        page.querySelector('#AtsLibrarySnapshotExport').addEventListener('click', exportLibrarySnapshot);
        mappingsApplyButton.addEventListener('click', applyMappingsImport);
        mappingsJsonInput.addEventListener('input', function () {
            parseMappingsJson(false);
        });
        mappingsFileInput.addEventListener('change', handleMappingsFile);
        page.querySelector('#AtsExplorerRefresh').addEventListener('click', function () {
            state.mappingExplorerRows = [];
            loadAllSeasonMappings().catch(function () { });
        });
        explorerSearch.addEventListener('input', renderMappingExplorer);
        explorerStatus.addEventListener('change', renderMappingExplorer);
        page.querySelector('#AnimeThemesBrowserPlayerClose').addEventListener('click', closePlayer);
        player.addEventListener('click', function (event) {
            if (event.target === player) closePlayer();
        });
        page.querySelector('#AnimeThemesDownloadDialogClose').addEventListener('click', closeDownloadDialog);
        page.querySelector('#AnimeThemesDownloadDialogCancel').addEventListener('click', closeDownloadDialog);
        downloadDialogConfirm.addEventListener('click', confirmDownloadSelection);
        downloadDialog.addEventListener('click', function (event) {
            if (event.target === downloadDialog) closeDownloadDialog();
        });
        [downloadIncludeVideo, downloadIncludeAudio, downloadIncludeExtras].forEach(function (control) {
            control.addEventListener('change', function () {
                downloadDialogError.hidden = true;
                syncDownloadOptionStyles();
            });
            var option = control.closest('.ats-download-option');
            option.addEventListener('click', function (event) {
                if (!control.parentElement.contains(event.target)) control.click();
            });
        });

        // Delete dialog setup
        page.querySelector('#AnimeThemesDeleteDialogClose').addEventListener('click', closeDeleteDialog);
        page.querySelector('#AnimeThemesDeleteDialogCancel').addEventListener('click', closeDeleteDialog);
        deleteDialogConfirm.addEventListener('click', confirmDeleteSelection);
        deleteDialog.addEventListener('click', function (event) {
            if (event.target === deleteDialog) closeDeleteDialog();
        });
        [deleteIncludeVideo, deleteIncludeAudio, deleteIncludeExtras].forEach(function (control) {
            if (control) {
                control.addEventListener('change', function () {
                    deleteDialogError.hidden = true;
                    syncDeleteOptionStyles();
                });
                var option = control.closest('.ats-download-option');
                if (option) {
                    option.addEventListener('click', function (event) {
                        if (!control.parentElement.contains(event.target)) control.click();
                    });
                }
            }
        });

        page.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                if (downloadDialog.classList.contains('open')) closeDownloadDialog();
                if (deleteDialog.classList.contains('open')) closeDeleteDialog();
            }
        });

        // Download manager setup
        var dmHeader = page.querySelector('.ats-dm-header');
        if (dmHeader) {
            dmHeader.addEventListener('click', function (event) {
                if (!event.target.closest('button')) toggleDownloadManager();
            });
        }
        if (dmToggle) {
            dmToggle.addEventListener('click', function (event) {
                event.stopPropagation();
                toggleDownloadManager();
            });
        }
        if (dmClearHistory) {
            dmClearHistory.addEventListener('click', function (event) {
                event.stopPropagation();
                clearFinishedDownloadHistory();
            });
        }

        setupClearButtons();
        syncSeasonFilterButtons();
        page.addEventListener('pageshow', function () {
            setupThemeObserver();
            if (state.detailLoading) showDetailLoading();
            setViewMode(state.viewMode);
            setViewSize(state.viewSize);
            setActiveTab(state.activeTab);
            attachScrollListener();
            if (state.items.length) {
                scheduleUiRefresh({ finder: state.activeTab === 'finder', mappings: state.activeTab === 'manage' });
            } else {
                loadItems();
            }
            startDownloadsPolling();
        });
        page.addEventListener('pagehide', function () {
            if (scrollListenerContainer) scrollListenerContainer.removeEventListener('scroll', handleScroll);
            scrollListenerAttached = false;
            scrollListenerContainer = null;
            if (state.browserObserver) state.browserObserver.disconnect();
            state.browserObserver = null;
            stopDownloadsPolling();
            if (state.uiRefreshTimer) clearTimeout(state.uiRefreshTimer);
            state.uiRefreshTimer = null;
            hideDetailLoading();
            teardownThemeObserver();
        });
    }

    return function (view, params) {
        setup(view);

        view.addEventListener('viewshow', function () {
            var event = document.createEvent('Event');
            event.initEvent('pageshow', true, true);
            view.dispatchEvent(event);
        });

        view.addEventListener('viewhide', function () {
            var event = document.createEvent('Event');
            event.initEvent('pagehide', true, true);
            view.dispatchEvent(event);
        });
    };
});
