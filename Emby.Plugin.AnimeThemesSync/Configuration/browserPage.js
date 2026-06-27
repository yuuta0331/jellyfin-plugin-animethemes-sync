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
            selectedSeason: null,
            selectedAnime: null,
            finderPreview: null,
            finderSearchTimer: null,
            finderSearchToken: 0,
            finderAutoSearched: {},
            importConfig: null,
            mappingsImportRows: null,
            settingsConfig: null,
            settingsLoaded: false,
            settingsSnapshot: '',
            settingsApplying: false,
            pendingDownload: null,
            browserStartIndex: 0,
            browserLimit: 80,
            browserTotalRecordCount: 0,
            browserCacheVersion: '',
            browserCacheReady: false,
            browserRebuildRunning: false,
            browserRefreshTimer: null,
            librarySearchTimer: null
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

        function apiGet(path) {
            return ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl(path), dataType: 'json' });
        }

        function apiPost(path) {
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(path), dataType: 'json' });
        }

        function apiPostJson(path, data) {
            return ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl(path),
                dataType: 'json',
                contentType: 'application/json',
                data: JSON.stringify(data || {})
            });
        }

        function apiDelete(path) {
            return ApiClient.ajax({ type: 'DELETE', url: ApiClient.getUrl(path), dataType: 'json' });
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
            if (err.responseJSON && err.responseJSON.error) return err.responseJSON.error;
            if (err.responseText) return err.responseText;
            return String(err);
        }

        function setImage(img, path, fallback) {
            img.classList.remove('ats-fade-in');
            img.style.display = 'none';
            img.removeAttribute('src');
            if (fallback) fallback.style.display = '';
            if (!path) return;
            img.onload = function () {
                img.style.display = 'block';
                img.classList.add('ats-fade-in');
                if (fallback) fallback.style.display = 'none';
            };
            img.onerror = function () {
                img.style.display = 'none';
                if (fallback) fallback.style.display = '';
            };
            img.src = apiUrl(path, false);
        }

        function setBackdrop(path) {
            backdrop.style.backgroundImage = path ? 'url("' + apiUrl(path, false) + '")' : '';
        }

        function selectedItem() {
            var id = itemSelect.value;
            return state.items.find(function (item) { return String(value(item, 'Id', 'id')) === id; }) || null;
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

        function renderItemOptions() {
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
            renderItemGrid();
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

        function renderItemGrid() {
            if (state.itemsLoading) {
                renderLibrarySkeleton();
                return;
            }

            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize + ' ' + (state.showLibraryDetails ? 'show-details' : 'hide-details');
            itemGrid.innerHTML = '';
            if (itemPager) itemPager.innerHTML = '';
            libraryCount.textContent = state.items.length + ' / ' + state.browserTotalRecordCount + ' items' + (state.browserRebuildRunning ? ' | updating' : '');
            if (!state.filteredItems.length) {
                appendEmptyState(
                    itemGrid,
                    state.browserCacheReady ? 'No library items found' : 'Updating library...',
                    state.browserCacheReady ? 'Try a different search or refresh the library.' : 'The library view will update automatically.');
                return;
            }

            state.filteredItems.forEach(function (item) {
                itemGrid.appendChild(createItemCard(item));
            });

            if (state.items.length < state.browserTotalRecordCount) {
                var more = document.createElement('button');
                more.type = 'button';
                more.className = 'raised emby-button ats-action-button ats-icon-button-text';
                more.textContent = 'Load more';
                more.addEventListener('click', function () {
                    loadItems(true);
                });
                (itemPager || itemGrid).appendChild(more);
            }
        }

        function createItemCard(item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-item-card ats-fade-in';
            var name = value(item, 'Name', 'name') || 'Unknown';
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
                if (!state.finderLoading && !state.seasonMappings.length) {
                    loadSeasonMappings().catch(function () { });
                } else {
                    renderMappingExplorer();
                }
            } else if (settingsActive) {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
                loadSettings(false);
            } else {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
            }
        }

        function openLibraryView() {
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
        }

        function openItemDetail(itemId) {
            if (!itemId) return;
            itemSelect.value = itemId;
            state.currentItem = selectedItem();
            state.currentResult = null;
            state.activeGroupId = null;
            state.detailLoading = true;
            state.detailError = null;
            state.detailToken++;
            setActiveTab('library');
            renderDetailLoading();
            syncLayout();
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

        function loadItems(append) {
            var startIndex = append ? state.items.length : 0;
            state.itemsLoading = true;
            state.summaryLoading = true;
            if (!append) renderLibrarySkeleton();
            renderSummarySkeleton();
            Dashboard.showLoadingMsg();
            Promise.all([apiGet(browserItemsPath(startIndex)), apiGet('AnimeThemesSync/Summary'), apiGet('AnimeThemesSync/Storage')]).then(function (results) {
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
                renderItemOptions();
                renderSummary(summary);
                renderStorage(storage);
                scheduleBrowserRefresh();
                Dashboard.hideLoadingMsg();
            }).catch(function (err) {
                state.itemsLoading = false;
                state.summaryLoading = false;
                itemGrid.innerHTML = '';
                appendEmptyState(itemGrid, 'Library failed to load', getErrorMessage(err));
                Dashboard.hideLoadingMsg();
                Dashboard.alert({ title: 'Browser Error', message: 'Failed to load items: ' + err });
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
                loadItems(false);
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

        function upsertSeasonMappingRow(row) {
            if (!row) return;
            var rowId = seasonRowId(row);
            var replaced = false;
            state.seasonMappings = state.seasonMappings.map(function (existing) {
                if (String(seasonRowId(existing)) === String(rowId)) {
                    replaced = true;
                    return row;
                }

                return existing;
            });
            if (!replaced) {
                state.seasonMappings.push(row);
            }
            state.selectedSeason = row;
            renderSeasonMappings();
            renderMappingExplorer();
        }

        function loadSeasonMappings() {
            var selectedId = state.selectedSeason ? seasonRowId(state.selectedSeason) : null;
            state.finderLoading = true;
            finderState.textContent = 'Loading season mappings...';
            renderFinderSkeleton(seasonList);
            return apiGet('AnimeThemesSync/SeasonMappings').then(function (rows) {
                state.finderLoading = false;
                state.seasonMappings = rows || [];
                if (selectedId) {
                    state.selectedSeason = state.seasonMappings.find(function (row) {
                        return String(seasonRowId(row)) === String(selectedId);
                    }) || state.selectedSeason;
                }
                renderSeasonMappings();
                renderMappingExplorer();
                finderState.textContent = state.seasonMappings.length + ' seasons loaded.';
                return state.seasonMappings;
            }).catch(function (err) {
                state.finderLoading = false;
                seasonList.innerHTML = '';
                appendEmptyState(seasonList, 'Season mappings failed to load', getErrorMessage(err));
                renderMappingExplorer();
                finderState.textContent = 'Failed to load season mappings.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
                return [];
            });
        }

        function renderSeasonMappings() {
            if (state.finderLoading) {
                renderFinderSkeleton(seasonList);
                return;
            }

            var filter = seasonFilter.value || 'unmatched';
            syncSeasonFilterButtons();
            var rows = state.seasonMappings.filter(function (row) {
                var status = String(value(row, 'Status', 'status') || '').toLowerCase();
                if (filter === 'all') return true;
                if (filter === 'auto') return status === 'auto' || status === 'direct' || status === 'series';
                return status === filter;
            });
            seasonList.innerHTML = '';
            if (!rows.length) {
                appendEmptyState(seasonList, 'No seasons match this filter', 'Switch filters or refresh season mappings.');
                return;
            }

            rows.forEach(function (row) {
                seasonList.appendChild(createSeasonCard(row));
            });
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
            loadSeasonMappings().then(function (rows) {
                var row = (rows || []).find(function (candidate) {
                    return String(seasonRowId(candidate)) === String(seasonItemId);
                });
                if (!row) {
                    finderState.textContent = 'Season was not found in Season Finder.';
                    return;
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
            addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl', 'animeThemesUrl'), true);
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
                if (row) {
                    seasonFilter.value = 'manual';
                    upsertSeasonMappingRow(row);
                }
                if (downloadAfterSave) {
                    startDownloadJob('AnimeThemesSync/Jobs/ItemDownload?ItemId=' + encodeURIComponent(payload.SeasonItemId) + '&Force=false');
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
                state.selectedAnime = null;
                state.finderPreview = null;
                finderPreview.className = 'ats-finder-preview fieldDescription';
                finderPreview.textContent = 'Mapping cleared.';
                if (row) {
                    seasonFilter.value = 'unmatched';
                    upsertSeasonMappingRow(row);
                } else {
                    loadSeasonMappings();
                }
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
            if (cacheBytes) cacheBytes.textContent = formatBytes(value(storage, 'DatabaseBytes', 'databaseBytes'));
            if (cacheItems) cacheItems.textContent = text(value(storage, 'BrowserItemCount', 'browserItemCount'));
            if (cacheState) cacheState.textContent = rebuilding ? 'Updating' : (ready ? 'Ready' : 'Starting');
            if (cachePath) {
                var path = value(storage, 'DatabasePath', 'databasePath');
                var lastError = value(storage, 'LastError', 'lastError');
                cachePath.textContent = lastError ? ('Cache file: ' + path + ' | Last error: ' + lastError) : ('Cache file: ' + path);
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
                loadItems();
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
                var rows = (state.seasonMappings || []).filter(seasonMappingHasMatch).map(function (row) {
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
            if (!state.seasonMappings.length) {
                loadSeasonMappings().then(doExport);
            } else {
                doExport();
            }
        }

        function exportLibrarySnapshot() {
            var doExport = function () {
                var content = JSON.stringify({ Rows: state.seasonMappings || [] }, null, 2);
                downloadJsonFile('animethemes-sync-library-snapshot.json', content);
                setMappingsState('Library snapshot exported.');
            };
            if (!state.seasonMappings.length) {
                loadSeasonMappings().then(doExport);
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
                loadSeasonMappings().catch(function () { });
                loadItems();
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
            var rows = state.seasonMappings || [];
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
            setBackdrop(null);
            poster.style.display = 'none';
            logo.style.display = 'none';
            posterFallback.style.display = 'none';
            title.textContent = 'Loading item...';
            meta.innerHTML = '';
            meta.appendChild(createSkeleton('ats-skeleton-line'));
            seasonGroups.style.display = '';
            seasonGroups.innerHTML = '';
            for (var i = 0; i < 4; i++) {
                var pill = document.createElement('div');
                pill.className = 'ats-season-pill ats-placeholder-card';
                pill.appendChild(createSkeleton('ats-skeleton-line wide'));
                pill.appendChild(createSkeleton('ats-skeleton-line'));
                seasonGroups.appendChild(pill);
            }
            rowsContainer.innerHTML = '';
            for (var row = 0; row < 4; row++) {
                var card = document.createElement('div');
                card.className = 'ats-theme-card ats-placeholder-card';
                card.appendChild(createSkeleton('ats-skeleton-line wide'));
                card.appendChild(createSkeleton('ats-skeleton-line'));
                card.appendChild(createSkeleton('ats-skeleton-line short'));
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

        function loadThemes(token) {
            var itemId = itemSelect.value;
            if (!itemId) return;
            state.currentItem = selectedItem();
            var previousGroupId = state.activeGroupId;
            apiGet('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes').then(function (result) {
                if (token && token !== state.detailToken) return;
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
                state.detailLoading = false;
                state.currentResult = null;
                state.detailError = getErrorMessage(err);
                renderDetailError(state.detailError);
                syncLayout();
                Dashboard.alert({ title: 'Browser Error', message: 'Failed to load themes: ' + err });
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
        }

        function createThemeCard(row) {
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

            // 1. AnimeThemes Link (icon-only)
            addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl', 'animeThemesUrl'), true);

            // 2. Play/Preview Video Stack & Individual Delete Video
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
            addDeleteFileButton(actions, row, 'video', 'Delete Video', value(row, 'BackdropExists', 'backdropExists'), true);

            // 3. Play/Preview Audio Stack & Individual Delete Audio
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
            addDeleteFileButton(actions, row, 'audio', 'Delete Audio', value(row, 'ThemeMusicExists', 'themeMusicExists'), true);

            // 4. Delete Extras
            addDeleteFileButton(actions, row, 'extra', 'Delete Extras', value(row, 'ExtraExists', 'extraExists'), true);

            // 5. Download button (icon-only, aligned to the right)
            addDownloadButton(actions, row, true);

            side.appendChild(actions);
            card.appendChild(side);
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
            if (iconOnly) {
                button.classList.add('ats-icon-button-only');
                button.classList.add('ats-button-align-right');
                button.title = 'Download theme';
            }
            button.addEventListener('click', function () {
                downloadTheme(row);
            });
            container.appendChild(button);
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
            config.ConfigurationVersion = 3;
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
                ConfigurationVersion: 3,
                ThemeDownloadingEnabled: true,
                MaxConcurrentDownloads: 1,
                DownloadTimeoutSeconds: 600,
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
                ConfigurationVersion: 3,
                ThemeDownloadingEnabled: !!getConfigValue(config, 'ThemeDownloadingEnabled', true),
                MaxConcurrentDownloads: Math.max(1, parseInt(getConfigValue(config, 'MaxConcurrentDownloads', 1), 10) || 1),
                DownloadTimeoutSeconds: Math.max(1, parseInt(getConfigValue(config, 'DownloadTimeoutSeconds', 600), 10) || 600),
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
                ConfigurationVersion: 3,
                ThemeDownloadingEnabled: settingsFields.ThemeDownloadingEnabled.checked,
                MaxConcurrentDownloads: parseInt(settingsFields.MaxConcurrentDownloads.value, 10) || 1,
                DownloadTimeoutSeconds: parseInt(settingsFields.DownloadTimeoutSeconds.value, 10) || 600,
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
                '&IncludeExtras=' + encodeURIComponent(downloadIncludeExtras.checked);
            closeDownloadDialog();
            startDownloadJob(path);
        }

        function downloadItem() {
            var itemId = activeGroupItemId();
            if (!itemId) return;
            startDownloadJob('AnimeThemesSync/Jobs/ItemDownload?ItemId=' + encodeURIComponent(itemId) + '&Force=false');
        }

        function startDownloadJob(path) {
            setProgress(true, 'Starting...', 0);
            apiPost(path).then(function (job) {
                pollDownloadJob(value(job, 'JobId', 'jobId'));
            }).catch(function (err) {
                setProgress(true, 'Failed to start: ' + getErrorMessage(err), 0);
                Dashboard.alert({ title: 'Download Error', message: 'Failed to start download: ' + getErrorMessage(err) });
            });
        }

        function pollDownloadJob(jobId) {
            if (!jobId) {
                setProgress(true, 'Failed to start: no job id returned', 0);
                return;
            }

            apiGet('AnimeThemesSync/Jobs/' + encodeURIComponent(jobId)).then(function (job) {
                var status = value(job, 'Status', 'status');
                var progress = value(job, 'Progress', 'progress') || 0;
                var message = value(job, 'Message', 'message') || status || 'Running';
                        setProgress(true, message, progress);
                        if (status === 'Completed') {
                            if (state.activeTab === 'finder') {
                                loadSeasonMappings();
                            } else if (state.activeTab === 'library') {
                                loadThemes();
                            } else if (state.activeTab === 'settings') {
                                loadSettings(true);
                            }
                            setTimeout(function () { setProgress(false, '', 0); }, 1600);
                            return;
                        }

                if (status === 'Failed') {
                    var error = value(job, 'Error', 'error') || 'Download failed.';
                    setProgress(true, error, progress);
                    Dashboard.alert({ title: 'Download Error', message: error });
                    return;
                }

                setTimeout(function () { pollDownloadJob(jobId); }, 1000);
            }).catch(function (err) {
                setProgress(true, 'Failed to read progress: ' + getErrorMessage(err), 0);
                Dashboard.alert({ title: 'Download Error', message: 'Failed to read download progress: ' + getErrorMessage(err) });
            });
        }

        function deleteThemeFiles(scope) {
            var label = scope === 'all' ? 'all AnimeThemes files' : scope === 'audio' ? 'theme songs' : 'theme videos and extras';
            if (!window.confirm('Delete ' + label + '?')) {
                return;
            }

            setProgress(true, 'Deleting ' + label + '...', 0);
            apiPost('AnimeThemesSync/ThemeFiles/Delete?Scope=' + encodeURIComponent(scope)).then(function (result) {
                var files = value(result, 'FilesDeleted', 'filesDeleted') || 0;
                var bytes = value(result, 'BytesDeleted', 'bytesDeleted') || 0;
                setProgress(true, 'Deleted ' + files + ' files (' + formatBytes(bytes) + ')', 100);
                loadItems();
                if (itemSelect.value) {
                    loadThemes();
                }
                setTimeout(function () { setProgress(false, '', 0); }, 1800);
            }).catch(function (err) {
                setProgress(true, 'Delete failed: ' + getErrorMessage(err), 0);
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

            setProgress(true, 'Deleting ' + label + '...', 0);
            apiPost('AnimeThemesSync/ThemeFiles/DeleteFile?ItemId=' + encodeURIComponent(itemId) + '&RowId=' + encodeURIComponent(rowId) + '&Target=' + encodeURIComponent(target)).then(function (result) {
                var files = value(result, 'FilesDeleted', 'filesDeleted') || 0;
                var bytes = value(result, 'BytesDeleted', 'bytesDeleted') || 0;
                setProgress(true, 'Deleted ' + files + ' files (' + formatBytes(bytes) + ')', 100);
                loadItems();
                loadThemes();
                setTimeout(function () { setProgress(false, '', 0); }, 1800);
            }).catch(function (err) {
                setProgress(true, 'Delete failed: ' + getErrorMessage(err), 0);
                Dashboard.alert({ title: 'Delete Error', message: getErrorMessage(err) });
            });
        }

        function openPlayer(row, target) {
            var rowId = value(row, 'RowId', 'rowId');
            var itemId = activeGroupItemId();
            state.lastFocus = document.activeElement;
            var src = apiUrl('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes/' + encodeURIComponent(rowId) + '/LocalMedia?target=' + encodeURIComponent(target) + '&_=' + Date.now(), true);
            playerTitle.textContent = text(value(row, 'ThemeKey', 'themeKey')) + ' - ' + target;
            playerBody.innerHTML = '';
            var element = document.createElement(target === 'audio' ? 'audio' : 'video');
            element.controls = true;
            element.autoplay = true;
            element.src = src;
            playerBody.appendChild(element);
            player.classList.add('open');
            player.setAttribute('aria-hidden', 'false');
        }

        function openRemotePlayer(row, target, src) {
            state.lastFocus = document.activeElement;
            playerTitle.textContent = text(value(row, 'ThemeKey', 'themeKey')) + ' - preview ' + target;
            playerBody.innerHTML = '';
            var element = document.createElement(target === 'audio' ? 'audio' : 'video');
            element.controls = true;
            element.autoplay = true;
            element.src = src;
            playerBody.appendChild(element);
            player.classList.add('open');
            player.setAttribute('aria-hidden', 'false');
        }

        function closePlayer() {
            if (player.contains(document.activeElement)) {
                document.activeElement.blur();
            }

            player.classList.remove('open');
            player.setAttribute('aria-hidden', 'true');
            playerBody.innerHTML = '';
            if (state.lastFocus && typeof state.lastFocus.focus === 'function') {
                state.lastFocus.focus();
            }
        }

        page.querySelectorAll('.ats-tab').forEach(function (button) {
            button.addEventListener('click', function () {
                setActiveTab(button.getAttribute('data-ats-tab') || 'library');
            });
        });
        page.querySelector('#AnimeThemesSeasonRefresh').addEventListener('click', loadSeasonMappings);
        seasonFilter.addEventListener('change', function () {
            syncSeasonFilterButtons();
            loadSeasonMappings();
        });
        page.querySelectorAll('[data-season-filter]').forEach(function (button) {
            button.addEventListener('click', function () {
                seasonFilter.value = button.getAttribute('data-season-filter') || 'unmatched';
                syncSeasonFilterButtons();
                loadSeasonMappings();
            });
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
            loadSeasonMappings().catch(function () { });
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
        page.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && downloadDialog.classList.contains('open')) {
                closeDownloadDialog();
            }
        });
        setupClearButtons();
        syncSeasonFilterButtons();
        page.addEventListener('pageshow', function () {
            setViewMode(state.viewMode);
            setViewSize(state.viewSize);
            setActiveTab(state.activeTab);
            loadItems();
        });
    }

    return function (view, params) {
        setup(view);

        view.addEventListener('viewshow', function () {
            var event = document.createEvent('Event');
            event.initEvent('pageshow', true, true);
            view.dispatchEvent(event);
        });
    };
});
