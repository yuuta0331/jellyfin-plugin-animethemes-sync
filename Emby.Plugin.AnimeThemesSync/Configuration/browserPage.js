define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function (BaseView) {
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
            activeTab: 'library',
            seasonMappings: [],
            selectedSeason: null,
            selectedAnime: null,
            finderPreview: null,
            finderSearchTimer: null,
            finderSearchToken: 0,
            finderAutoSearched: {},
            settingsConfig: null,
            settingsLoaded: false
        };
        var browserToolbar = page.querySelector('.ats-browser-toolbar');
        var itemSelect = page.querySelector('#AnimeThemesBrowserItemSelect');
        var libraryView = page.querySelector('#AnimeThemesBrowserLibraryView');
        var detailView = page.querySelector('#AnimeThemesBrowserDetailView');
        var manageView = page.querySelector('#AnimeThemesBrowserManageView');
        var finderView = page.querySelector('#AnimeThemesSeasonFinderView');
        var settingsView = page.querySelector('#AnimeThemesBrowserSettingsView');
        var itemGrid = page.querySelector('#AnimeThemesBrowserItemGrid');
        var libraryCount = page.querySelector('#AnimeThemesBrowserLibraryCount');
        var rowsContainer = page.querySelector('#AnimeThemesBrowserRows');
        var seasonGroups = page.querySelector('#AnimeThemesBrowserSeasonGroups');
        var searchInput = page.querySelector('#AnimeThemesBrowserSearch');
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
        var progressPanel = page.querySelector('#AnimeThemesBrowserProgress');
        var progressText = page.querySelector('#AnimeThemesBrowserProgressText');
        var progressPercent = page.querySelector('#AnimeThemesBrowserProgressPercent');
        var progressBar = page.querySelector('#AnimeThemesBrowserProgressBar');
        var summaryItems = page.querySelector('#AnimeThemesSummaryItems');
        var summaryVideos = page.querySelector('#AnimeThemesSummaryVideos');
        var summarySongs = page.querySelector('#AnimeThemesSummarySongs');
        var summaryExtras = page.querySelector('#AnimeThemesSummaryExtras');
        var summaryBytes = page.querySelector('#AnimeThemesSummaryBytes');
        var seasonFilter = page.querySelector('#AnimeThemesSeasonFilter');
        var seasonList = page.querySelector('#AnimeThemesSeasonList');
        var finderSearchInput = page.querySelector('#AnimeThemesFinderSearchInput');
        var finderYear = page.querySelector('#AnimeThemesFinderYear');
        var finderResults = page.querySelector('#AnimeThemesFinderResults');
        var finderPreview = page.querySelector('#AnimeThemesFinderPreview');
        var finderState = page.querySelector('#AnimeThemesFinderState');
        var downloadItemButton = page.querySelector('#AnimeThemesBrowserDownload');
        var settingsState = page.querySelector('#AnimeThemesSettingsState');
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
            ExtrasFileNameFormat: page.querySelector('#AtsExtrasFileNameFormat'),
            TagsEnabled: page.querySelector('#AtsTagsEnabled'),
            TagOptions: page.querySelector('#AtsTagOptions'),
            TagFormat: page.querySelector('#AtsTagFormat'),
            TagSeasonSpring: page.querySelector('#AtsTagSeasonSpring'),
            TagSeasonSummer: page.querySelector('#AtsTagSeasonSummer'),
            TagSeasonFall: page.querySelector('#AtsTagSeasonFall'),
            TagSeasonWinter: page.querySelector('#AtsTagSeasonWinter'),
            CustomCssText: page.querySelector('#AtsCustomCssText'),
            CopyCssButton: page.querySelector('#AtsCopyCssButton'),
            CopyCssMessage: page.querySelector('#AtsCopyCssMessage')
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
            return String(value(group, 'ItemId', 'itemId') || '') + ':' + String(value(group, 'Type', 'type') || '');
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

        function itemMatchesSearch(item) {
            var query = searchInput.value.trim().toLowerCase();
            if (!query) return true;
            return [
                value(item, 'Name', 'name'),
                value(item, 'AnimeThemesSlug', 'animeThemesSlug'),
                value(item, 'AniListId', 'aniListId'),
                value(item, 'MyAnimeListId', 'myAnimeListId')
            ].join(' ').toLowerCase().indexOf(query) !== -1;
        }

        function renderItemOptions() {
            var previous = itemSelect.value;
            state.filteredItems = state.items.filter(itemMatchesSearch);
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
            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize;
            itemGrid.innerHTML = '';
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
            [summaryItems, summaryVideos, summarySongs, summaryExtras, summaryBytes].forEach(function (node) {
                node.innerHTML = '<span class="ats-skeleton ats-skeleton-text"></span>';
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

            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize;
            itemGrid.innerHTML = '';
            libraryCount.textContent = state.filteredItems.length + ' / ' + state.items.length + ' items';
            if (!state.filteredItems.length) {
                appendEmptyState(itemGrid, 'No library items found', 'Try a different search or refresh the library list.');
                return;
            }

            state.filteredItems.forEach(function (item) {
                itemGrid.appendChild(createItemCard(item));
            });
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

            if (state.viewMode === 'list') {
                var textWrap = document.createElement('div');
                appendDiv(textWrap, 'ats-item-title', name);
                appendDiv(textWrap, 'ats-item-meta', [
                    value(item, 'Type', 'type'),
                    value(item, 'AnimeThemesSlug', 'animeThemesSlug') || value(item, 'AniListId', 'aniListId') || value(item, 'MyAnimeListId', 'myAnimeListId')
                ].filter(Boolean).join(' | '));
                button.appendChild(textWrap);
            }

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
                loadSeasonMappings();
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

        function loadItems() {
            state.itemsLoading = true;
            state.summaryLoading = true;
            renderLibrarySkeleton();
            renderSummarySkeleton();
            Dashboard.showLoadingMsg();
            Promise.all([apiGet('AnimeThemesSync/Items'), apiGet('AnimeThemesSync/Summary')]).then(function (results) {
                var items = results[0];
                var summary = results[1] || {};
                state.items = items || [];
                state.itemsLoading = false;
                state.summaryLoading = false;
                renderItemOptions();
                renderSummary(summary);
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
        }

        function loadSeasonMappings() {
            var selectedId = state.selectedSeason ? seasonRowId(state.selectedSeason) : null;
            state.finderLoading = true;
            finderState.textContent = 'Loading season mappings...';
            renderFinderSkeleton(seasonList);
            apiGet('AnimeThemesSync/SeasonMappings').then(function (rows) {
                state.finderLoading = false;
                state.seasonMappings = rows || [];
                if (selectedId) {
                    state.selectedSeason = state.seasonMappings.find(function (row) {
                        return String(seasonRowId(row)) === String(selectedId);
                    }) || state.selectedSeason;
                }
                renderSeasonMappings();
                finderState.textContent = state.seasonMappings.length + ' seasons loaded.';
            }).catch(function (err) {
                state.finderLoading = false;
                seasonList.innerHTML = '';
                appendEmptyState(seasonList, 'Season mappings failed to load', getErrorMessage(err));
                finderState.textContent = 'Failed to load season mappings.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
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
            addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl', 'animeThemesUrl'));
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

        function addRemotePreviewButton(container, row, target) {
            var url = target === 'audio' ? value(row, 'AudioUrl', 'audioUrl') : value(row, 'VideoUrl', 'videoUrl');
            var button = createButton(target === 'audio' ? 'Preview Audio' : 'Preview Video', true, 'play');
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
            summaryVideos.textContent = text(value(summary, 'ThemeVideos', 'themeVideos'));
            summarySongs.textContent = text(value(summary, 'ThemeSongs', 'themeSongs'));
            summaryExtras.textContent = text(value(summary, 'Extras', 'extras'));
            summaryBytes.textContent = formatBytes(value(summary, 'TotalBytes', 'totalBytes'));
        }

        function renderDetailLoading() {
            detailView.classList.add('ats-detail-loading');
            downloadItemButton.disabled = true;
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
            var rows = activeRows().filter(rowMatches);
            rowsContainer.innerHTML = '';
            if (!rows.length) {
                appendEmptyState(rowsContainer, 'No themes to show', value(group, 'EmptyMessage', 'emptyMessage') || 'Adjust filters or choose another season.');
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
            addRemotePreviewButton(actions, row, 'video');
            addRemotePreviewButton(actions, row, 'audio');
            addDownloadButton(actions, row);
            addLocalVideoButton(actions, row);
            addPlayButton(actions, row, 'audio', 'Play Audio', value(row, 'SavedAudioPlayable', 'savedAudioPlayable'));
            addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl', 'animeThemesUrl'));
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

        function createButton(label, secondary, tone) {
            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.type = 'button';
            button.className = secondary ? 'emby-button ats-button-secondary' : 'raised emby-button ats-action-button';
            if (tone) {
                button.className += ' ats-button-' + tone;
            }
            var labelSpan = document.createElement('span');
            labelSpan.textContent = label;
            button.appendChild(labelSpan);
            return button;
        }

        function addDownloadButton(container, row) {
            var button = createButton('Download', false, 'download');
            button.addEventListener('click', function () {
                downloadTheme(row);
            });
            container.appendChild(button);
        }

        function addLocalVideoButton(container, row) {
            var hasExtra = !!value(row, 'SavedExtraPlayable', 'savedExtraPlayable');
            var hasVideo = !!value(row, 'SavedVideoPlayable', 'savedVideoPlayable');
            var button = createButton('Play Video', true, 'play');
            button.disabled = !hasExtra && !hasVideo;
            button.title = hasExtra ? 'Plays the browseable extras file.' : hasVideo ? 'Plays the local theme video file.' : 'No local video has been saved.';
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, hasExtra ? 'extra' : 'video');
            });
            container.appendChild(button);
        }

        function addPlayButton(container, row, target, label, playable) {
            var button = createButton(label, true, 'play');
            button.disabled = !playable;
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, target);
            });
            container.appendChild(button);
        }

        function addOpenButton(container, label, url) {
            if (!url) return;
            var button = createButton(label, true, 'link');
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
            config.ConfigurationVersion = 2;
            config.Series = ensureMediaConfig(getConfigValue(config, 'Series', null));
            config.Movie = ensureMediaConfig(getConfigValue(config, 'Movie', null));
            if (!Array.isArray(getConfigValue(config, 'SeasonThemeMappings', []))) {
                config.SeasonThemeMappings = [];
            }
            return config;
        }

        function normalizeExtrasLinkMode(value) {
            if (value === 'HardLinkOnly') return '1';
            if (value === 'CopyOnly') return '2';
            if (value === 'HardLinkWithCopyFallback') return '0';
            return String(value || 0);
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

        function applySettingsToForm(config) {
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
            settingsFields.ExtrasFileNameFormat.value = getConfigValue(config, 'ExtrasFileNameFormat', '{Order}. {Theme} - {Song}');
            settingsFields.TagsEnabled.checked = !!getConfigValue(config, 'TagsEnabled', true);
            settingsFields.TagFormat.value = getConfigValue(config, 'TagFormat', '{Season} {Year}');
            settingsFields.TagSeasonSpring.value = getConfigValue(config, 'TagSeasonSpring', 'Spring');
            settingsFields.TagSeasonSummer.value = getConfigValue(config, 'TagSeasonSummer', 'Summer');
            settingsFields.TagSeasonFall.value = getConfigValue(config, 'TagSeasonFall', 'Fall');
            settingsFields.TagSeasonWinter.value = getConfigValue(config, 'TagSeasonWinter', 'Winter');
            syncConditionalSettings();
            renderAllProfileSettings();
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

        function collectSettingsFromForm(config) {
            applyAllProfileSettings();
            config = ensureSettingsConfig(config || state.settingsConfig);
            config.ConfigurationVersion = 2;
            config.ThemeDownloadingEnabled = settingsFields.ThemeDownloadingEnabled.checked;
            config.MaxConcurrentDownloads = parseInt(settingsFields.MaxConcurrentDownloads.value, 10) || 1;
            config.DownloadTimeoutSeconds = parseInt(settingsFields.DownloadTimeoutSeconds.value, 10) || 600;
            config.AllowAdd = settingsFields.AllowAdd.checked;
            config.ForceRedownload = settingsFields.ForceRedownload.checked;
            config.AllowDelete = settingsFields.AllowDelete.checked;
            config.SeasonThemeDownloadsEnabled = settingsFields.SeasonThemeDownloadsEnabled.checked;
            config.ExtrasEnabled = settingsFields.ExtrasEnabled.checked;
            config.ExtrasLinkMode = parseInt(settingsFields.ExtrasLinkMode.value, 10) || 0;
            config.ExtrasFileNameFormat = settingsFields.ExtrasFileNameFormat.value;
            config.TagsEnabled = settingsFields.TagsEnabled.checked;
            config.TagFormat = settingsFields.TagFormat.value;
            config.TagSeasonSpring = settingsFields.TagSeasonSpring.value;
            config.TagSeasonSummer = settingsFields.TagSeasonSummer.value;
            config.TagSeasonFall = settingsFields.TagSeasonFall.value;
            config.TagSeasonWinter = settingsFields.TagSeasonWinter.value;
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
            setSettingsState('Saving settings...');
            return ApiClient.getPluginConfiguration(pluginUniqueId).then(function (config) {
                config = collectSettingsFromForm(config || {});
                return ApiClient.updatePluginConfiguration(pluginUniqueId, config).then(function (result) {
                    state.settingsLoaded = true;
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
            saveSettings(false).then(function () {
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

        function downloadTheme(row) {
            var itemId = activeGroupItemId();
            var rowId = value(row, 'RowId', 'rowId');
            if (!itemId || !rowId) return;
            var force = page.querySelector('#AnimeThemesBrowserForce').checked;
            startDownloadJob('AnimeThemesSync/Jobs/ThemeDownload?ItemId=' + encodeURIComponent(itemId) + '&RowId=' + encodeURIComponent(rowId) + '&Force=' + force);
        }

        function downloadItem() {
            var itemId = activeGroupItemId();
            if (!itemId) return;
            var force = page.querySelector('#AnimeThemesBrowserForce').checked;
            startDownloadJob('AnimeThemesSync/Jobs/ItemDownload?ItemId=' + encodeURIComponent(itemId) + '&Force=' + force);
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
            renderItemOptions();
        });
        themeSearchInput.addEventListener('input', renderThemes);
        typeFilter.addEventListener('change', renderThemes);
        statusFilter.addEventListener('change', renderThemes);
        flagFilter.addEventListener('change', renderThemes);
        gridSizeSelect.addEventListener('change', function () {
            setViewSize(gridSizeSelect.value);
        });
        itemSelect.addEventListener('change', function () {
            if (itemSelect.value) openItemDetail(itemSelect.value);
        });
        page.querySelector('#AnimeThemesBrowserRefreshItems').addEventListener('click', loadItems);
        page.querySelector('#AnimeThemesBrowserDownload').addEventListener('click', downloadItem);
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
        syncConditionalSettings();
        page.querySelector('#AtsSettingsSave').addEventListener('click', function () {
            saveSettings(true);
        });
        page.querySelector('#AtsRunTask').addEventListener('click', runScheduledTask);
        if (settingsFields.CopyCssButton) {
            settingsFields.CopyCssButton.addEventListener('click', copyCustomCss);
        }
        page.querySelector('#AnimeThemesBrowserPlayerClose').addEventListener('click', closePlayer);
        player.addEventListener('click', function (event) {
            if (event.target === player) closePlayer();
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
