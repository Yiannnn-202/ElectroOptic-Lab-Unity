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

    // ---- exclude formula blocks ----
    function isInsideProtected(el) {
        if (!el) return false;
        var parent = el.parentElement;
        while (parent) {
            if (parent.classList.contains("formula-box")) return true;
            parent = parent.parentElement;
        }
        return false;
    }

    var btn = document.getElementById("editToggle");
    var editing = false;
    var editableElements = [];
    var tableEditCells = [];

    // ---- toggle editing ----
    function enterEditMode() {
        editing = true;
        btn.classList.add("active");
        btn.innerHTML = "&#10003;";
        btn.title = "完成编辑";
        document.body.classList.add("editing");

        var candidates = document.querySelectorAll(EDITABLE_SELECTORS);
        for (var i = 0; i < candidates.length; i++) {
            if (isInsideProtected(candidates[i])) continue;
            candidates[i].contentEditable = "true";
            editableElements.push(candidates[i]);
        }

        // data-table body cells (exclude row-num, delete-cell, readonly-cell)
        var dataCells = document.querySelectorAll(
            ".data-table tbody td:not(.row-num):not(.delete-cell):not(.readonly-cell)"
        );
        for (var j = 0; j < dataCells.length; j++) {
            dataCells[j].contentEditable = "true";
            tableEditCells.push(dataCells[j]);
        }

        // data-table column headers
        var colHeaders = document.querySelectorAll(".data-table thead .data-header");
        for (var ch = 0; ch < colHeaders.length; ch++) {
            colHeaders[ch].contentEditable = "true";
            tableEditCells.push(colHeaders[ch]);
        }

        // enable Vπ inputs
        var vpiInputs = document.querySelectorAll(".vpi-input");
        for (var k = 0; k < vpiInputs.length; k++) {
            vpiInputs[k].readOnly = false;
        }
    }

    function exitEditMode() {
        editing = false;
        btn.classList.remove("active");
        btn.innerHTML = "&#9998;";
        btn.title = "编辑报告";
        document.body.classList.remove("editing");

        for (var i = 0; i < editableElements.length; i++) {
            editableElements[i].contentEditable = "inherit";
        }
        editableElements = [];

        for (var j = 0; j < tableEditCells.length; j++) {
            tableEditCells[j].contentEditable = "false";
        }
        tableEditCells = [];

        var vpiInputs = document.querySelectorAll(".vpi-input");
        for (var k = 0; k < vpiInputs.length; k++) {
            vpiInputs[k].readOnly = true;
        }
    }

    btn.addEventListener("click", function () {
        if (editing) { exitEditMode(); } else { enterEditMode(); }
    });

    // ---- keyboard shortcuts ----
    document.addEventListener("keydown", function (e) {
        if (e.ctrlKey && e.key === "e") {
            e.preventDefault();
            if (editing) { exitEditMode(); } else { enterEditMode(); }
        }
        if (e.key === "Escape" && editing) {
            e.preventDefault();
            exitEditMode();
        }
    });

    // ---- export HTML ----
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
            } catch (_) { }
        }
        return css;
    }

    function exportReport() {
        if (editing) exitEditMode();

        var clone = document.documentElement.cloneNode(true);

        var toolbar = clone.querySelector(".toolbar");
        if (toolbar) toolbar.remove();

        var scripts = clone.querySelectorAll("script");
        for (var i = 0; i < scripts.length; i++) scripts[i].remove();

        var body = clone.querySelector("body");
        if (body) body.classList.remove("editing");
        var editables = clone.querySelectorAll("[contenteditable]");
        for (var j = 0; j < editables.length; j++) editables[j].removeAttribute("contenteditable");

        // remove UI controls
        var uiControls = clone.querySelectorAll(
            ".add-row-btn, .delete-row-btn, .remove-img-btn, .delete-cell"
        );
        for (var c = 0; c < uiControls.length; c++) uiControls[c].remove();

        // hide upload drop areas
        var dropAreas = clone.querySelectorAll(".upload-drop-area");
        for (var d = 0; d < dropAreas.length; d++) dropAreas[d].style.display = "none";
        var previewWraps = clone.querySelectorAll(".upload-preview-wrap");
        for (var p = 0; p < previewWraps.length; p++) previewWraps[p].style.display = "block";

        // remove "操作" column headers
        var opHeaders = clone.querySelectorAll(".op-header");
        for (var oh = 0; oh < opHeaders.length; oh++) opHeaders[oh].remove();

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

    // ---- PDF export via browser print ----
    document.getElementById("pdfBtn").addEventListener("click", function () {
        if (editing) exitEditMode();
        window.print();
    });

    // ====================================================================
    //  Image Upload
    // ====================================================================
    function initImageUpload(zoneId) {
        var zone = document.getElementById(zoneId);
        if (!zone) return;

        var fileInput = zone.querySelector(".file-input");
        var dropArea = zone.querySelector(".upload-drop-area");
        var previewWrap = zone.querySelector(".upload-preview-wrap");
        var previewImg = zone.querySelector(".preview-img");
        var removeBtn = zone.querySelector(".remove-img-btn");

        function showPreview(dataUrl) {
            previewImg.src = dataUrl;
            dropArea.style.display = "none";
            previewWrap.style.display = "block";
        }

        function hidePreview() {
            previewImg.src = "";
            dropArea.style.display = "";
            previewWrap.style.display = "none";
            fileInput.value = "";
        }

        function loadFile(file) {
            if (!file || !file.type.startsWith("image/")) return;
            var reader = new FileReader();
            reader.onload = function (e) { showPreview(e.target.result); };
            reader.readAsDataURL(file);
        }

        dropArea.addEventListener("click", function () { fileInput.click(); });

        fileInput.addEventListener("change", function () {
            if (fileInput.files && fileInput.files[0]) loadFile(fileInput.files[0]);
        });

        dropArea.addEventListener("dragover", function (e) {
            e.preventDefault();
            dropArea.classList.add("drag-over");
        });
        dropArea.addEventListener("dragleave", function () {
            dropArea.classList.remove("drag-over");
        });
        dropArea.addEventListener("drop", function (e) {
            e.preventDefault();
            dropArea.classList.remove("drag-over");
            if (e.dataTransfer.files && e.dataTransfer.files[0]) loadFile(e.dataTransfer.files[0]);
        });

        zone.addEventListener("dragover", function (e) {
            e.preventDefault();
            dropArea.classList.add("drag-over");
        });
        zone.addEventListener("drop", function (e) {
            if (e.target === zone || e.target === previewWrap || e.target === previewImg) {
                e.preventDefault();
                dropArea.classList.remove("drag-over");
                if (e.dataTransfer.files && e.dataTransfer.files[0]) loadFile(e.dataTransfer.files[0]);
            }
        });

        removeBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            hidePreview();
        });

        previewImg.addEventListener("click", function () { fileInput.click(); });
    }

    // ====================================================================
    //  Data Table — fixed columns, add/delete rows only
    // ====================================================================
    function initDataTable(tableId, defaultRows) {
        var table = document.getElementById(tableId);
        if (!table) return;

        var tbody = table.querySelector("tbody");
        var headerRow = table.querySelector("thead tr");

        function countDataCols() {
            var ths = headerRow.querySelectorAll("th");
            var count = 0;
            for (var i = 0; i < ths.length; i++) {
                if (!ths[i].classList.contains("num-header") &&
                    !ths[i].classList.contains("op-header")) {
                    count++;
                }
            }
            return count;
        }

        function renumber() {
            var rows = tbody.querySelectorAll("tr");
            for (var i = 0; i < rows.length; i++) {
                var numCell = rows[i].querySelector(".row-num");
                if (numCell) numCell.textContent = i + 1;
            }
        }

        function createRow() {
            var tr = document.createElement("tr");

            var numTd = document.createElement("td");
            numTd.className = "row-num";
            numTd.textContent = "—";
            tr.appendChild(numTd);

            var colCount = countDataCols();
            for (var c = 0; c < colCount; c++) {
                var td = document.createElement("td");
                tr.appendChild(td);
            }

            tbody.appendChild(tr);
            renumber();

            if (editing) {
                var newCells = tr.querySelectorAll("td:not(.row-num):not(.readonly-cell)");
                for (var nc = 0; nc < newCells.length; nc++) {
                    newCells[nc].contentEditable = "true";
                    tableEditCells.push(newCells[nc]);
                }
            }
        }

        // populate initial rows
        for (var r = 0; r < defaultRows; r++) createRow();

        // "add row" button
        var addBtn = document.querySelector('.add-row-btn[data-table="' + tableId + '"]');
        if (addBtn) {
            addBtn.addEventListener("click", function () { createRow(); });
        }
    }

    // ====================================================================
    //  Summary Sync — copy Vπ inputs to summary table & conclusion
    // ====================================================================
    function syncField(sourceId, targetId) {
        var src = document.getElementById(sourceId);
        var tgt = document.getElementById(targetId);
        if (!src || !tgt) return;
        src.addEventListener("input", function () {
            var val = src.value.trim();
            tgt.textContent = val || "—";
            if (targetId.indexOf("Vpi") !== -1 && val) {
                tgt.textContent = val + " V";
            } else if (targetId.indexOf("Error") !== -1 && val) {
                tgt.textContent = val + "%";
            } else if (!val) {
                tgt.textContent = "—";
            }
        });
    }

    function initSync() {
        // 5.1 → 6.3 summary + 7 conclusion
        syncField("dcVpi", "summaryDcVpi");
        syncField("dcVpi", "finalDcVpi");

        // 5.2 → 6.3 summary + 7 conclusion
        syncField("acVpi", "summaryAcVpi");
        syncField("acVpi", "finalAcVpi");

        // 6.3 theory Vπ → 7 conclusion
        syncField("theoryVpi", "finalTheoryVpi");
    }

    // ====================================================================
    //  Init
    // ====================================================================
    function initAll() {
        initImageUpload("conoscopicZone");
        initImageUpload("dcCurveZone");
        initImageUpload("acWaveZone");
        // dcDataTable: 5 data cols (U₁, P₁, U₂, P₂, |U₂-U₁|), 3 default rows
        initDataTable("dcDataTable", 3);
        // acDataTable: 3 data cols (倍频点n, 倍频点n+1, |ΔU|), 5 default rows
        initDataTable("acDataTable", 5);
        initSync();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }
})();
