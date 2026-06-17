define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function (BaseView) {
    'use strict';

    function setup(view) {
        if (view.getAttribute('data-ats-script-bound') === 'true') {
            return;
        }

        view.setAttribute('data-ats-script-bound', 'true');

        var page = view;
        var state = { items: [], filteredItems: [], currentItem: null, currentResult: null, viewMode: 'poster' };
        var itemSelect = page.querySelector('#AnimeThemesBrowserItemSelect');
        var libraryView = page.querySelector('#AnimeThemesBrowserLibraryView');
        var detailView = page.querySelector('#AnimeThemesBrowserDetailView');
        var itemGrid = page.querySelector('#AnimeThemesBrowserItemGrid');
        var libraryCount = page.querySelector('#AnimeThemesBrowserLibraryCount');
        var rowsContainer = page.querySelector('#AnimeThemesBrowserRows');
        var searchInput = page.querySelector('#AnimeThemesBrowserSearch');
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

        function apiUrl(path, authenticated) {
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
            itemGrid.className = 'ats-item-grid ' + state.viewMode;
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
            appendDiv(textWrap, 'ats-item-title', name);
            appendDiv(textWrap, 'ats-item-meta', [
                value(item, 'Type', 'type'),
                value(item, 'AnimeThemesSlug', 'animeThemesSlug') || value(item, 'AniListId', 'aniListId') || value(item, 'MyAnimeListId', 'myAnimeListId')
            ].filter(Boolean).join(' | '));
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
            state.viewMode = mode;
            page.querySelectorAll('.ats-view-mode').forEach(function (button) {
                button.classList.toggle('active', button.getAttribute('data-view-mode') === mode);
            });
            renderItemGrid();
        }

        function openLibraryView() {
            detailView.style.display = 'none';
            libraryView.style.display = '';
            state.currentItem = null;
            state.currentResult = null;
            itemSelect.value = '';
        }

        function openItemDetail(itemId) {
            if (!itemId) return;
            itemSelect.value = itemId;
            libraryView.style.display = 'none';
            detailView.style.display = '';
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
                link.setAttribute('is', 'emby-linkbutton');
                link.className = 'button-link';
                link.target = '_blank';
                link.rel = 'noopener';
                link.href = url;
                link.textContent = 'Open AnimeThemes';
                meta.appendChild(document.createTextNode('  '));
                meta.appendChild(link);
            }
        }

        function rowMatches(row) {
            var query = searchInput.value.trim().toLowerCase();
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

        function createButton(label, secondary) {
            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.type = 'button';
            button.className = secondary ? 'emby-button ats-button-secondary' : 'raised emby-button ats-action-button';
            var labelSpan = document.createElement('span');
            labelSpan.textContent = label;
            button.appendChild(labelSpan);
            return button;
        }

        function addDownloadButton(container, row) {
            var button = createButton('Download', false);
            button.addEventListener('click', function () {
                downloadTheme(row);
            });
            container.appendChild(button);
        }

        function addLocalVideoButton(container, row) {
            var hasExtra = !!value(row, 'SavedExtraPlayable', 'savedExtraPlayable');
            var hasVideo = !!value(row, 'SavedVideoPlayable', 'savedVideoPlayable');
            var button = createButton('Play Video', true);
            button.disabled = !hasExtra && !hasVideo;
            button.title = hasExtra ? 'Plays the browseable extras file.' : hasVideo ? 'Plays the local theme video file.' : 'No local video has been saved.';
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, hasExtra ? 'extra' : 'video');
            });
            container.appendChild(button);
        }

        function addPlayButton(container, row, target, label, playable) {
            var button = createButton(label, true);
            button.disabled = !playable;
            button.addEventListener('click', function () {
                if (!button.disabled) openPlayer(row, target);
            });
            container.appendChild(button);
        }

        function addOpenButton(container, label, url) {
            if (!url) return;
            var button = createButton(label, true);
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
                    loadThemes();
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

        searchInput.addEventListener('input', function () {
            renderItemOptions();
            if (state.currentResult) renderThemes();
        });
        typeFilter.addEventListener('change', renderThemes);
        statusFilter.addEventListener('change', renderThemes);
        flagFilter.addEventListener('change', renderThemes);
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
