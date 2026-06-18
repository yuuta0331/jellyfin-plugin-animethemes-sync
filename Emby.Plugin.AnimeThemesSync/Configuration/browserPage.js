define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function (BaseView) {
    'use strict';

    function setup(view) {
        if (view.getAttribute('data-ats-script-bound') === 'true') {
            return;
        }

        view.setAttribute('data-ats-script-bound', 'true');

        var page = view;
        var state = {
            items: [],
            filteredItems: [],
            currentItem: null,
            currentResult: null,
            viewMode: 'poster',
            viewSize: 'medium',
            activeTab: 'library',
            seasonMappings: [],
            selectedSeason: null,
            selectedAnime: null,
            finderPreview: null,
            finderSearchTimer: null,
            finderSearchToken: 0,
            finderAutoSearched: {}
        };
        var browserToolbar = page.querySelector('.ats-browser-toolbar');
        var itemSelect = page.querySelector('#AnimeThemesBrowserItemSelect');
        var libraryView = page.querySelector('#AnimeThemesBrowserLibraryView');
        var detailView = page.querySelector('#AnimeThemesBrowserDetailView');
        var manageView = page.querySelector('#AnimeThemesBrowserManageView');
        var itemGrid = page.querySelector('#AnimeThemesBrowserItemGrid');
        var libraryCount = page.querySelector('#AnimeThemesBrowserLibraryCount');
        var rowsContainer = page.querySelector('#AnimeThemesBrowserRows');
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
            img.style.display = 'none';
            img.removeAttribute('src');
            if (fallback) fallback.style.display = '';
            if (!path) return;
            img.onload = function () {
                img.style.display = 'block';
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

        function renderItemGrid() {
            itemGrid.className = 'ats-item-grid ' + state.viewMode + ' size-' + state.viewSize;
            itemGrid.innerHTML = '';
            libraryCount.textContent = state.filteredItems.length + ' / ' + state.items.length + ' items';
            if (!state.filteredItems.length) {
                var empty = document.createElement('div');
                empty.className = 'fieldDescription';
                empty.textContent = 'No library items match the current search.';
                itemGrid.appendChild(empty);
                return;
            }

            state.filteredItems.forEach(function (item) {
                itemGrid.appendChild(createItemCard(item));
            });
        }

        function createItemCard(item) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-item-card';
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
            state.activeTab = tab === 'manage' ? 'manage' : 'library';
            syncLayout();
        }

        function syncLayout() {
            page.querySelectorAll('.ats-tab').forEach(function (button) {
                button.classList.toggle('active', button.getAttribute('data-ats-tab') === state.activeTab);
            });
            var libraryActive = state.activeTab === 'library';
            var detailActive = libraryActive && !!state.currentResult;
            browserToolbar.style.display = libraryActive && !detailActive ? '' : 'none';
            filtersPanel.style.display = detailActive ? '' : 'none';
            manageView.style.display = libraryActive ? 'none' : '';
            if (libraryActive) {
                detailView.style.display = detailActive ? '' : 'none';
                libraryView.style.display = detailActive ? 'none' : '';
            } else {
                libraryView.style.display = 'none';
                detailView.style.display = 'none';
                loadSeasonMappings();
            }
        }

        function openLibraryView() {
            detailView.style.display = 'none';
            libraryView.style.display = state.activeTab === 'library' ? '' : 'none';
            state.currentItem = null;
            state.currentResult = null;
            itemSelect.value = '';
            syncLayout();
        }

        function openItemDetail(itemId) {
            if (!itemId) return;
            itemSelect.value = itemId;
            state.currentItem = selectedItem();
            state.currentResult = {};
            setActiveTab('library');
            loadThemes();
        }

        function loadItems() {
            Dashboard.showLoadingMsg();
            Promise.all([apiGet('AnimeThemesSync/Items'), apiGet('AnimeThemesSync/Summary')]).then(function (results) {
                var items = results[0];
                var summary = results[1] || {};
                state.items = items || [];
                renderItemOptions();
                renderSummary(summary);
                Dashboard.hideLoadingMsg();
            }).catch(function (err) {
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
            finderState.textContent = 'Loading season mappings...';
            apiGet('AnimeThemesSync/SeasonMappings').then(function (rows) {
                state.seasonMappings = rows || [];
                if (selectedId) {
                    state.selectedSeason = state.seasonMappings.find(function (row) {
                        return String(seasonRowId(row)) === String(selectedId);
                    }) || state.selectedSeason;
                }
                renderSeasonMappings();
                finderState.textContent = state.seasonMappings.length + ' seasons loaded.';
            }).catch(function (err) {
                finderState.textContent = 'Failed to load season mappings.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
        }

        function renderSeasonMappings() {
            var filter = seasonFilter.value || 'unmatched';
            var rows = state.seasonMappings.filter(function (row) {
                var status = String(value(row, 'Status', 'status') || '').toLowerCase();
                if (filter === 'all') return true;
                if (filter === 'auto') return status === 'auto' || status === 'direct' || status === 'series';
                return status === filter;
            });
            seasonList.innerHTML = '';
            if (!rows.length) {
                appendDiv(seasonList, 'fieldDescription', 'No seasons match the current filter.');
                return;
            }

            rows.forEach(function (row) {
                seasonList.appendChild(createSeasonCard(row));
            });
        }

        function createSeasonCard(row) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-season-card';
            button.classList.toggle('selected', state.selectedSeason && value(state.selectedSeason, 'SeasonItemId', 'seasonItemId') === value(row, 'SeasonItemId', 'seasonItemId'));
            appendDiv(button, 'ats-item-title', text(value(row, 'SeriesName', 'seriesName')) + ' / ' + text(value(row, 'SeasonName', 'seasonName')));
            appendDiv(button, 'ats-item-meta', [
                'Season ' + text(value(row, 'SeasonNumber', 'seasonNumber')),
                text(value(row, 'Status', 'status')),
                text(value(row, 'AnimeName', 'animeName') || value(row, 'AnimeThemesSlug', 'animeThemesSlug'))
            ].join(' | '));
            var chips = document.createElement('div');
            chips.className = 'ats-status-list';
            addChip(chips, text(value(row, 'Source', 'source')), '');
            if (value(row, 'SameAsSeries', 'sameAsSeries')) addChip(chips, 'Series-level', 'ok');
            if (!value(row, 'AnimeThemesSlug', 'animeThemesSlug')) addChip(chips, 'Needs match', 'missing');
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
            finderResults.innerHTML = '';
            apiGet('AnimeThemesSync/Search?query=' + encodeURIComponent(query) + (year ? '&year=' + encodeURIComponent(year) : '')).then(function (results) {
                if (token !== state.finderSearchToken) return;
                renderSearchResults(results || []);
                finderState.textContent = (results || []).length + ' candidates found.';
            }).catch(function (err) {
                if (token !== state.finderSearchToken) return;
                finderState.textContent = 'Search failed.';
                Dashboard.alert({ title: 'Season Finder Error', message: getErrorMessage(err) });
            });
        }

        function renderSearchResults(results) {
            finderResults.innerHTML = '';
            if (!results.length) {
                appendDiv(finderResults, 'fieldDescription', 'No candidates found.');
                return;
            }

            results.forEach(function (result) {
                finderResults.appendChild(createSearchCard(result));
            });
        }

        function createSearchCard(result) {
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'ats-search-card';
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
            apiGet('AnimeThemesSync/Anime/' + encodeURIComponent(slug) + '/Themes').then(function (preview) {
                state.finderPreview = preview || {};
                renderFinderPreview();
                finderState.textContent = 'Preview loaded.';
            }).catch(function (err) {
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
                appendDiv(list, 'fieldDescription', 'No themes found for this candidate.');
            } else {
                rows.slice(0, 12).forEach(function (row) {
                    list.appendChild(createPreviewThemeRow(row));
                });
            }
            finderPreview.appendChild(list);
        }

        function createPreviewThemeRow(row) {
            var card = document.createElement('div');
            card.className = 'ats-theme-card';
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
                    var targetItemId = value(state.selectedSeason, 'SeriesItemId', 'seriesItemId') || payload.SeasonItemId;
                    startDownloadJob('AnimeThemesSync/Jobs/ItemDownload?ItemId=' + encodeURIComponent(targetItemId) + '&Force=false');
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

        function loadThemes() {
            var itemId = itemSelect.value;
            if (!itemId) return;
            state.currentItem = selectedItem();
            Dashboard.showLoadingMsg();
            apiGet('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes').then(function (result) {
                Dashboard.hideLoadingMsg();
                state.currentResult = result || {};
                renderHero();
                renderThemes();
                syncLayout();
            }).catch(function (err) {
                Dashboard.hideLoadingMsg();
                Dashboard.alert({ title: 'Browser Error', message: 'Failed to load themes: ' + err });
            });
        }

        function renderHero() {
            var item = state.currentItem || {};
            var result = state.currentResult || {};
            title.textContent = text(value(result, 'Name', 'name')) + ' - Themes';
            var name = value(result, 'Name', 'name') || value(item, 'Name', 'name') || 'AT';
            posterFallback.textContent = String(name).trim().slice(0, 2).toUpperCase();
            setImage(poster, value(item, 'PrimaryImageUrl', 'primaryImageUrl'), posterFallback);
            setImage(logo, value(item, 'LogoImageUrl', 'logoImageUrl'), null);
            setBackdrop(value(item, 'BackdropImageUrl', 'backdropImageUrl') || value(item, 'ThumbImageUrl', 'thumbImageUrl'));

            meta.innerHTML = '';
            var count = (value(result, 'Themes', 'themes') || []).length;
            var countSpan = document.createElement('span');
            countSpan.textContent = count + ' themes';
            meta.appendChild(countSpan);
            var url = value(result, 'AnimeThemesUrl', 'animeThemesUrl');
            if (url) {
                var link = document.createElement('a');
                link.className = 'button-link ats-inline-link';
                link.target = '_blank';
                link.rel = 'noopener';
                link.href = url;
                link.textContent = 'Open AnimeThemes';
                meta.appendChild(document.createTextNode('  '));
                meta.appendChild(link);
            }
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
            var rows = (value(state.currentResult, 'Themes', 'themes') || []).filter(rowMatches);
            rowsContainer.innerHTML = '';
            if (!rows.length) {
                var empty = document.createElement('div');
                empty.className = 'fieldDescription';
                empty.textContent = 'No themes match the current filters.';
                rowsContainer.appendChild(empty);
                return;
            }

            rows.forEach(function (row) {
                rowsContainer.appendChild(createThemeCard(row));
            });
        }

        function createThemeCard(row) {
            var card = document.createElement('div');
            card.className = 'ats-theme-card';

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

        function downloadTheme(row) {
            var itemId = itemSelect.value;
            var rowId = value(row, 'RowId', 'rowId');
            if (!itemId || !rowId) return;
            var force = page.querySelector('#AnimeThemesBrowserForce').checked;
            startDownloadJob('AnimeThemesSync/Jobs/ThemeDownload?ItemId=' + encodeURIComponent(itemId) + '&RowId=' + encodeURIComponent(rowId) + '&Force=' + force);
        }

        function downloadItem() {
            var itemId = itemSelect.value;
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
                    if (state.activeTab === 'manage') {
                        loadSeasonMappings();
                    } else {
                        loadThemes();
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
            var itemId = itemSelect.value;
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
        seasonFilter.addEventListener('change', loadSeasonMappings);
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
        page.querySelector('#AnimeThemesBrowserPlayerClose').addEventListener('click', closePlayer);
        player.addEventListener('click', function (event) {
            if (event.target === player) closePlayer();
        });
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
