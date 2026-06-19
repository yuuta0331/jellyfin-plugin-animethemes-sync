define(['baseView'], function (BaseView) {
    'use strict';

    function View(view) {
        BaseView.apply(this, arguments);
        this.view = view;
    }

    Object.assign(View.prototype, BaseView.prototype);

    return View;
});
