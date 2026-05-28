(function () {
    "use strict";

    // ---- editable text selectors ----
    var EDITABLE_SELECTORS = [
        "h1",
        ".subtitle",
        ".meta-grid strong",
        ".meta-grid span",
        "h2",
        "h3",
        "h4",
        "p",
        "li"
    ].join(", ");

    // ---- exclude formula / placeholder blocks ----
    function isInsideProtected(el) {
        if (!el) return false;
        var parent = el.parentElement;
        while (parent) {
            if (parent.classList.contains("formula-box") ||
                parent.classList.contains("placeholder-block")) {
                return true;
            }
            parent = parent.parentElement;
        }
        return false;
    }

    var btn = document.getElementById("editToggle");
    var editing = false;
    var editableElements = [];

    // ---- toggle editing ----
    function enterEditMode() {
        editing = true;
        btn.classList.add("active");
        btn.innerHTML = "&#10003;";      // checkmark
        btn.title = "完成编辑";
        document.body.classList.add("editing");

        var candidates = document.querySelectorAll(EDITABLE_SELECTORS);
        for (var i = 0; i < candidates.length; i++) {
            if (isInsideProtected(candidates[i])) continue;
            candidates[i].contentEditable = "true";
            editableElements.push(candidates[i]);
        }
    }

    function exitEditMode() {
        editing = false;
        btn.classList.remove("active");
        btn.innerHTML = "&#9998;";       // pencil
        btn.title = "切换编辑模式";
        document.body.classList.remove("editing");

        for (var i = 0; i < editableElements.length; i++) {
            editableElements[i].contentEditable = "inherit";
        }
        editableElements = [];
    }

    btn.addEventListener("click", function () {
        if (editing) {
            exitEditMode();
        } else {
            enterEditMode();
        }
    });

    // ---- keyboard shortcut: Ctrl+E ----
    document.addEventListener("keydown", function (e) {
        if (e.ctrlKey && e.key === "e") {
            e.preventDefault();
            if (editing) {
                exitEditMode();
            } else {
                enterEditMode();
            }
        }
        // Esc to exit edit mode
        if (e.key === "Escape" && editing) {
            e.preventDefault();
            exitEditMode();
        }
    });

    // ---- export ----
    function getCssText() {
        var css = "";
        for (var i = 0; i < document.styleSheets.length; i++) {
            try {
                var href = document.styleSheets[i].href || "";
                if (href.indexOf("report.css") === -1) continue;
                var rules = document.styleSheets[i].cssRules;
                for (var j = 0; j < rules.length; j++) {
                    css += rules[j].cssText + "\n";
                }
            } catch (_) { /* cross-origin, skip */ }
        }
        return css;
    }

    function exportReport() {
        if (editing) exitEditMode();

        var clone = document.documentElement.cloneNode(true);

        // remove toolbar
        var toolbar = clone.querySelector(".toolbar");
        if (toolbar) toolbar.remove();

        // remove scripts
        var scripts = clone.querySelectorAll("script");
        for (var i = 0; i < scripts.length; i++) scripts[i].remove();

        // remove editing artifacts
        var body = clone.querySelector("body");
        if (body) body.classList.remove("editing");
        var editables = clone.querySelectorAll("[contenteditable]");
        for (var j = 0; j < editables.length; j++) editables[j].removeAttribute("contenteditable");

        // inline CSS
        var style = document.createElement("style");
        style.textContent = getCssText();
        var head = clone.querySelector("head");
        var links = head.querySelectorAll('link[rel="stylesheet"]');
        for (var k = 0; k < links.length; k++) links[k].remove();
        head.appendChild(style);

        var html = "<!DOCTYPE html>\n" + clone.outerHTML;
        var blob = new Blob([html], { type: "text/html;charset=UTF-8" });
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.href = url;
        var now = new Date();
        var ds = now.getFullYear() + "-" +
            String(now.getMonth() + 1).padStart(2, "0") + "-" +
            String(now.getDate()).padStart(2, "0");
        a.download = "实验报告_" + ds + ".html";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    document.getElementById("exportBtn").addEventListener("click", exportReport);
})();
