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
    var tableEditCells = [];    // data-table cells separately tracked

    // ---- toggle editing ----
    function enterEditMode() {
        editing = true;
        btn.classList.add("active");
        btn.innerHTML = "&#10003;";      // checkmark
        btn.title = "完成编辑";
        document.body.classList.add("editing");

        // text elements (h1-h4, p, li, etc.)
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
        btn.innerHTML = "&#9998;";       // pencil
        btn.title = "切换编辑模式";
        document.body.classList.remove("editing");

        // text elements
        for (var i = 0; i < editableElements.length; i++) {
            editableElements[i].contentEditable = "inherit";
        }
        editableElements = [];

        // data-table cells
        for (var j = 0; j < tableEditCells.length; j++) {
            tableEditCells[j].contentEditable = "false";
        }
        tableEditCells = [];

        // disable Vπ inputs
        var vpiInputs = document.querySelectorAll(".vpi-input");
        for (var k = 0; k < vpiInputs.length; k++) {
            vpiInputs[k].readOnly = true;
        }
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

        // remove UI controls (add/delete row/col buttons, remove-img, delete cells)
        var uiControls = clone.querySelectorAll(
            ".add-row-btn, .add-col-btn, .delete-row-btn, .remove-img-btn, .col-remove-btn, .delete-cell"
        );
        for (var c = 0; c < uiControls.length; c++) uiControls[c].remove();

        // hide upload drop areas, show previews if any
        var dropAreas = clone.querySelectorAll(".upload-drop-area");
        for (var d = 0; d < dropAreas.length; d++) dropAreas[d].style.display = "none";
        var previewWraps = clone.querySelectorAll(".upload-preview-wrap");
        for (var p = 0; p < previewWraps.length; p++) previewWraps[p].style.display = "block";

        // remove "操作" column header (both rowspan and regular)
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

    // ====================================================================
    //  Image Upload — click / drag-drop → FileReader → base64 preview
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
            reader.onload = function (e) {
                showPreview(e.target.result);
            };
            reader.readAsDataURL(file);
        }

        // click to open file picker
        dropArea.addEventListener("click", function () {
            fileInput.click();
        });

        fileInput.addEventListener("change", function () {
            if (fileInput.files && fileInput.files[0]) {
                loadFile(fileInput.files[0]);
            }
        });

        // drag & drop
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
            if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                loadFile(e.dataTransfer.files[0]);
            }
        });

        // also allow dropping on the whole zone
        zone.addEventListener("dragover", function (e) {
            e.preventDefault();
            dropArea.classList.add("drag-over");
        });

        zone.addEventListener("drop", function (e) {
            // only handle if not already handled by dropArea
            if (e.target === zone || e.target === previewWrap || e.target === previewImg) {
                e.preventDefault();
                dropArea.classList.remove("drag-over");
                if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                    loadFile(e.dataTransfer.files[0]);
                }
            }
        });

        // remove button
        removeBtn.addEventListener("click", function (e) {
            e.stopPropagation();
            hidePreview();
        });

        // click preview to replace
        previewImg.addEventListener("click", function () {
            fileInput.click();
        });
    }

    // ====================================================================
    //  Data Table — add/delete rows & columns, auto-number
    // ====================================================================
    function initDataTable(tableId, defaultRows, expandable) {
        var table = document.getElementById(tableId);
        if (!table) return;

        var tbody = table.querySelector("tbody");
        var headerRow = table.querySelector("thead tr");
        table._expandable = !!expandable;
        table._colsPerAdd = 2;   // dcDataTable: add 偏置电压 + 功率计示数 per click

        // ---- count current data columns (all <th> except 序号 / 操作) ----
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

        // ---- renumber rows ----
        function renumber() {
            var rows = tbody.querySelectorAll("tr");
            for (var i = 0; i < rows.length; i++) {
                var numCell = rows[i].querySelector(".row-num");
                if (numCell) numCell.textContent = i + 1;
            }
        }

        // ---- create a single body row ----
        function createRow() {
            var tr = document.createElement("tr");

            // 序号 cell
            var numTd = document.createElement("td");
            numTd.className = "row-num";
            numTd.textContent = "—";
            tr.appendChild(numTd);

            // data cells (one per data-header)
            var colCount = countDataCols();
            for (var c = 0; c < colCount; c++) {
                var td = document.createElement("td");
                // cells are made contenteditable only in edit mode
                tr.appendChild(td);
            }

            // delete button cell
            var delTd = document.createElement("td");
            delTd.className = "delete-cell";
            var delBtn = document.createElement("button");
            delBtn.type = "button";
            delBtn.className = "delete-row-btn";
            delBtn.title = "删除此行";
            delBtn.textContent = "✕";
            delBtn.addEventListener("click", function () {
                if (tbody.querySelectorAll("tr").length <= 1) return;
                tr.remove();
                renumber();
            });
            delTd.appendChild(delBtn);
            tr.appendChild(delTd);

            tbody.appendChild(tr);
            renumber();

            // if currently in edit mode, make new cells editable
            if (editing) {
                var newCells = tr.querySelectorAll("td:not(.row-num):not(.delete-cell):not(.readonly-cell)");
                for (var nc = 0; nc < newCells.length; nc++) {
                    newCells[nc].contentEditable = "true";
                    tableEditCells.push(newCells[nc]);
                }
            }
        }

        // populate initial rows
        for (var r = 0; r < defaultRows; r++) {
            createRow();
        }

        // ---- "add row" button ----
        var addBtn = document.querySelector('.add-row-btn[data-table="' + tableId + '"]');
        if (addBtn) {
            addBtn.addEventListener("click", function () {
                createRow();
            });
        }

        // ---- wire up existing col-remove-btn in initial HTML ----
        var existingRmBtns = headerRow.querySelectorAll(".col-remove-btn");
        for (var rb = 0; rb < existingRmBtns.length; rb++) {
            existingRmBtns[rb].addEventListener("click", function (e) {
                e.stopPropagation();
                removeColumn(table);
            });
        }

        // ---- "add column" button (expandable tables only) ----
        if (expandable) {
            var addColBtn = document.querySelector('.add-col-btn[data-table="' + tableId + '"]');
            if (addColBtn) {
                addColBtn.addEventListener("click", function () {
                    addColumn(table);
                });
            }
        }
    }

    // ====================================================================
    //  Column — add / remove columns (pairs for expandable tables)
    // ====================================================================
    function addColumn(table) {
        if (!table._expandable) return;

        var headerRow = table.querySelector("thead tr");
        var opHeader = headerRow.querySelector(".op-header");
        var tbody = table.querySelector("tbody");
        var n = table._colsPerAdd || 1;

        // group number for naming (existing pairs + 1)
        var existing = headerRow.querySelectorAll(".data-header");
        var groupNum = Math.floor(existing.length / n) + 1;

        // default names per column in a group
        var names = ["偏置电压 U", "功率计示数 P"];

        for (var i = 0; i < n; i++) {
            var colName = names[i] || "列";
            colName += groupNum + (n > 1 && i === 0 ? " (V)" : n > 1 && i === 1 ? " (μW)" : "");

            var th = document.createElement("th");
            th.className = "data-header";
            th.textContent = colName;

            // remove button (only on first column of the group)
            if (i === 0 && groupNum > 1) {
                var rmBtn = document.createElement("button");
                rmBtn.type = "button";
                rmBtn.className = "col-remove-btn";
                rmBtn.title = "删除此组";
                rmBtn.textContent = "✕";
                rmBtn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    removeColumn(table);
                });
                th.appendChild(rmBtn);
            }

            headerRow.insertBefore(th, opHeader);

            if (editing) {
                th.contentEditable = "true";
                tableEditCells.push(th);
            }
        }

        // add n <td> cells per body row
        var rows = tbody.querySelectorAll("tr");
        for (var r = 0; r < rows.length; r++) {
            var delCell = rows[r].querySelector(".delete-cell");
            for (var c = 0; c < n; c++) {
                var td = document.createElement("td");
                rows[r].insertBefore(td, delCell);
                if (editing) {
                    td.contentEditable = "true";
                    tableEditCells.push(td);
                }
            }
        }
    }

    function removeColumn(table) {
        if (!table._expandable) return;

        var headerRow = table.querySelector("thead tr");
        var dataHeaders = headerRow.querySelectorAll(".data-header");
        var n = table._colsPerAdd || 1;
        if (dataHeaders.length <= n) return;   // keep initial columns

        var tbody = table.querySelector("tbody");

        // remove last n data-headers
        for (var i = 0; i < n; i++) {
            var lastH = headerRow.querySelector(".data-header:last-of-type");
            if (lastH) lastH.remove();
        }

        // remove last n data <td> from each body row
        var rows = tbody.querySelectorAll("tr");
        for (var r = 0; r < rows.length; r++) {
            for (var c = 0; c < n; c++) {
                // re-query each iteration — the DOM shrinks
                var fresh = rows[r].querySelectorAll("td:not(.row-num):not(.delete-cell):not(.readonly-cell)");
                if (fresh.length === 0) break;
                var lastTd = fresh[fresh.length - 1];
                var idx = tableEditCells.indexOf(lastTd);
                if (idx !== -1) tableEditCells.splice(idx, 1);
                lastTd.remove();
            }
        }
    }

    // ====================================================================
    //  Summary Sync — copy Vπ / error inputs to summary table & conclusion
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
        // 4.1 → 5.2 summary + conclusion
        syncField("dcVpi", "summaryDcVpi");
        syncField("dcVpi", "finalDcVpi");

        // 4.2 → 5.2 summary + conclusion
        syncField("acVpi", "summaryAcVpi");
        syncField("acVpi", "finalAcVpi");

        // 5.2 theory Vπ → conclusion
        syncField("theoryVpi", "finalTheoryVpi");

        // 5.2 error rates → conclusion
        syncField("dcErrorRate", "finalDcError");
        syncField("acErrorRate", "finalAcError");
    }

    // ====================================================================
    //  Init everything on DOM ready
    // ====================================================================
    function initAll() {
        initImageUpload("dcCurveZone");
        initImageUpload("acWaveZone");
        initDataTable("dcDataTable", 3, true);    // expandable: 4 data cols, 3 rows
        initDataTable("acDataTable", 5, false);   // fixed columns
        initSync();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initAll);
    } else {
        initAll();
    }

})();
