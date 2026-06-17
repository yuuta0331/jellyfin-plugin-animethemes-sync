define(['baseView', 'loading', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox', 'emby-textarea', 'emby-scroller'], function (BaseView) {
    'use strict';

    function setup(view) {
        if (view.getAttribute('data-ats-script-bound') === 'true') {
            return;
        }

        view.setAttribute('data-ats-script-bound', 'true');

(function () {
                var page = document.querySelector('#AnimeThemesBrowserPage');
                var itemSelect = page.querySelector('#AnimeThemesBrowserItemSelect');
                var rowsBody = page.querySelector('#AnimeThemesBrowserRows');
                var title = page.querySelector('#AnimeThemesBrowserTitle');
                var meta = page.querySelector('#AnimeThemesBrowserMeta');

                function value(obj, pascal, camel) {
                    return obj ? obj[pascal] !== undefined ? obj[pascal] : obj[camel] : null;
                }

                function text(value) {
                    return value === null || value === undefined || value === '' ? '-' : String(value);
                }

                function apiGet(path) {
                    return ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl(path), dataType: 'json' });
                }

                function apiPost(path) {
                    return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl(path), dataType: 'json' });
                }

                function loadItems() {
                    Dashboard.showLoadingMsg();
                    apiGet('AnimeThemesSync/Items').then(function (items) {
                        itemSelect.innerHTML = '';
                        (items || []).forEach(function (item) {
                            var option = document.createElement('option');
                            option.value = value(item, 'Id', 'id');
                            option.textContent = text(value(item, 'Name', 'name')) + ' (' + text(value(item, 'Type', 'type')) + ')';
                            itemSelect.appendChild(option);
                        });
                        Dashboard.hideLoadingMsg();
                        if (itemSelect.value) {
                            loadThemes();
                        }
                    }).catch(function (err) {
                        Dashboard.hideLoadingMsg();
                        Dashboard.alert({ title: 'Browser Error', message: 'Failed to load items: ' + err });
                    });
                }

                function loadThemes() {
                    var itemId = itemSelect.value;
                    if (!itemId) {
                        return;
                    }

                    Dashboard.showLoadingMsg();
                    apiGet('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/Themes').then(function (result) {
                        Dashboard.hideLoadingMsg();
                        renderThemes(result || {});
                    }).catch(function (err) {
                        Dashboard.hideLoadingMsg();
                        Dashboard.alert({ title: 'Browser Error', message: 'Failed to load themes: ' + err });
                    });
                }

                function renderThemes(result) {
                    var rows = value(result, 'Themes', 'themes') || [];
                    title.textContent = text(value(result, 'Name', 'name')) + ' - Themes';
                    var url = value(result, 'AnimeThemesUrl', 'animeThemesUrl');
                    meta.textContent = '';
                    if (url) {
                        var animeLink = document.createElement('a');
                        animeLink.setAttribute('is', 'emby-linkbutton');
                        animeLink.className = 'button-link';
                        animeLink.target = '_blank';
                        animeLink.rel = 'noopener';
                        animeLink.href = url;
                        animeLink.textContent = 'Open AnimeThemes';
                        meta.appendChild(animeLink);
                    }

                    rowsBody.innerHTML = '';

                    rows.forEach(function (row) {
                        var tr = document.createElement('tr');
                        var flags = [];
                        if (value(row, 'Spoiler', 'spoiler')) flags.push('Spoiler');
                        if (value(row, 'Nsfw', 'nsfw')) flags.push('NSFW');
                        var labels = value(row, 'Labels', 'labels');
                        if (labels) flags.push(labels);

                        appendCell(tr, text(value(row, 'ThemeKey', 'themeKey')));
                        appendCell(tr, text(value(row, 'SongTitle', 'songTitle')));
                        appendCell(tr, text(value(row, 'Artists', 'artists')));
                        appendCell(tr, text(value(row, 'Episodes', 'episodes')));
                        appendCell(tr, text(flags.join(', ')));
                        appendCell(tr, text(value(row, 'Quality', 'quality')));

                        var localCell = document.createElement('td');
                        addStatus(localCell, 'Video', value(row, 'BackdropExists', 'backdropExists'), value(row, 'BackdropPath', 'backdropPath'));
                        addStatus(localCell, 'Audio', value(row, 'ThemeMusicExists', 'themeMusicExists'), value(row, 'ThemeMusicPath', 'themeMusicPath'));
                        addStatus(localCell, 'Extras', value(row, 'ExtraExists', 'extraExists'), value(row, 'ExtraPath', 'extraPath'));
                        tr.appendChild(localCell);

                        var actionsCell = document.createElement('td');
                        var actions = document.createElement('div');
                        actions.className = 'theme-actions';
                        actionsCell.appendChild(actions);
                        tr.appendChild(actionsCell);
                        addOpenButton(actions, 'Video', value(row, 'VideoUrl', 'videoUrl'));
                        addOpenButton(actions, 'Audio', value(row, 'AudioUrl', 'audioUrl'));
                        addOpenButton(actions, 'AnimeThemes', value(row, 'AnimeThemesUrl', 'animeThemesUrl'));
                        rowsBody.appendChild(tr);
                    });
                }

                function appendCell(row, cellText) {
                    var td = document.createElement('td');
                    td.textContent = cellText;
                    row.appendChild(td);
                }

                function addStatus(container, label, exists, path) {
                    var span = document.createElement('span');
                    span.className = 'theme-status ' + (path && exists ? 'ok' : 'missing');
                    span.textContent = label + ': ' + (!path ? 'not selected' : exists ? 'saved' : 'missing');
                    if (!path) {
                        container.appendChild(span);
                        return;
                    }

                    span.title = path;
                    container.appendChild(span);
                }

                function addOpenButton(container, label, url) {
                    if (!url) {
                        return;
                    }

                    var button = document.createElement('button');
                    button.setAttribute('is', 'emby-button');
                    button.type = 'button';
                    button.className = 'button-flat emby-button';
                    var labelSpan = document.createElement('span');
                    labelSpan.textContent = label;
                    button.appendChild(labelSpan);
                    button.addEventListener('click', function () {
                        window.open(url, '_blank', 'noopener');
                    });
                    container.appendChild(button);
                }

                function downloadItem() {
                    var itemId = itemSelect.value;
                    if (!itemId) {
                        return;
                    }

                    var force = page.querySelector('#AnimeThemesBrowserForce').checked;
                    Dashboard.showLoadingMsg();
                    apiPost('AnimeThemesSync/Items/' + encodeURIComponent(itemId) + '/DownloadThemes?force=' + force).then(function () {
                        Dashboard.hideLoadingMsg();
                        loadThemes();
                    }).catch(function (err) {
                        Dashboard.hideLoadingMsg();
                        Dashboard.alert({ title: 'Download Error', message: 'Failed to download item: ' + err });
                    });
                }

                page.querySelector('#AnimeThemesBrowserRefreshItems').addEventListener('click', loadItems);
                page.querySelector('#AnimeThemesBrowserLoadThemes').addEventListener('click', loadThemes);
                page.querySelector('#AnimeThemesBrowserDownload').addEventListener('click', downloadItem);
                page.addEventListener('pageshow', loadItems);
            })();

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

