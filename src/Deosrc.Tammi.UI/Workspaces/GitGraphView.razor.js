import { attach as attachSplitterBar } from "../Components/Splitter.razor.js";

// The git graph's commit list. Up and Down select the next or previous commit (in .NET), so they mustn't also scroll the
// list; the list scrolls to keep the selected commit in view instead. Its header's handles resize the columns.

// The widths the user has given columns, by column, shared by every git graph while the app runs. A column that isn't
// here has its default width from the CSS (the graph's from its lanes).
const columnWidths = {};
const minColumnWidths = { graph: 14, author: 60, date: 60, sha: 50 };

export function attach(list, header) {
  const onKeyDown = (e) => {
    if (e.key === "ArrowUp" || e.key === "ArrowDown") {
      e.preventDefault();
    }
  };

  // The widths are set on the list's parent, which inherits them to the list: Blazor sets the list's own style, and
  // would replace them.
  const target = list.parentElement;
  const applyColumns = () => {
    for (const column of Object.keys(minColumnWidths)) {
      if (columnWidths[column] === undefined) {
        target.style.removeProperty(`--git-graph-col-${column}`);
      } else {
        target.style.setProperty(`--git-graph-col-${column}`, `${columnWidths[column]}px`);
      }
    }
  };

  const columnWidth = (handle) => handle.parentElement.getBoundingClientRect().width;

  const resize = (handle, width) => {
    const column = handle.dataset.column;
    columnWidths[column] = Math.round(Math.max(width, minColumnWidths[column]));
    applyColumns();
  };

  // A drag on a handle: its edge follows the pointer. `direction` is 1 for a handle on the column's right edge (moving
  // right widens it) and -1 for one on its left edge (moving right narrows it).
  let drag = null;

  const onPointerDown = (e) => {
    const handle = e.target.closest(".git-graph-column-handle");
    if (!handle || e.button !== 0) {
      return;
    }

    e.preventDefault();
    handle.setPointerCapture(e.pointerId);
    handle.classList.add("dragging");
    drag = { handle, startX: e.clientX, startWidth: columnWidth(handle), direction: Number(handle.dataset.direction) };
  };

  const onPointerMove = (e) => {
    if (drag) {
      resize(drag.handle, drag.startWidth + drag.direction * (e.clientX - drag.startX));
    }
  };

  const onPointerUp = (e) => {
    if (drag) {
      drag.handle.releasePointerCapture(e.pointerId);
      drag.handle.classList.remove("dragging");
      drag = null;
    }
  };

  // Left and Right move the focused handle's edge. The keys stop here, so they don't also move the selection.
  const onHeaderKeyDown = (e) => {
    const handle = e.target.closest(".git-graph-column-handle");
    if (!handle || (e.key !== "ArrowLeft" && e.key !== "ArrowRight")) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
    const step = (e.key === "ArrowRight" ? 16 : -16) * Number(handle.dataset.direction);
    resize(handle, columnWidth(handle) + step);
  };

  const onDoubleClick = (e) => {
    const handle = e.target.closest(".git-graph-column-handle");
    if (handle) {
      delete columnWidths[handle.dataset.column];
      applyColumns();
    }
  };

  // Clicks on the handles mustn't reach the list.
  const onClick = (e) => e.stopPropagation();

  list.addEventListener("keydown", onKeyDown);
  header.addEventListener("pointerdown", onPointerDown);
  header.addEventListener("pointermove", onPointerMove);
  header.addEventListener("pointerup", onPointerUp);
  header.addEventListener("pointercancel", onPointerUp);
  header.addEventListener("keydown", onHeaderKeyDown);
  header.addEventListener("dblclick", onDoubleClick);
  header.addEventListener("click", onClick);
  applyColumns();

  return {
    // Scrolls the row at `index` into view below the header, if it isn't already. Every row is `rowHeight` pixels high.
    reveal(index, rowHeight) {
      const offset = header.offsetHeight;
      const top = offset + index * rowHeight;
      const bottom = top + rowHeight;
      if (top - offset < list.scrollTop) {
        list.scrollTop = top - offset;
      } else if (bottom > list.scrollTop + list.clientHeight) {
        list.scrollTop = bottom - list.clientHeight;
      }
    },

    scrollToTop() {
      list.scrollTop = 0;
    },

    dispose() {
      list.removeEventListener("keydown", onKeyDown);
      header.removeEventListener("pointerdown", onPointerDown);
      header.removeEventListener("pointermove", onPointerMove);
      header.removeEventListener("pointerup", onPointerUp);
      header.removeEventListener("pointercancel", onPointerUp);
      header.removeEventListener("keydown", onHeaderKeyDown);
      header.removeEventListener("dblclick", onDoubleClick);
      header.removeEventListener("click", onClick);
    },
  };
}

// The details panel's default width, and the least it and the commit list are given.
const defaultDetailsWidth = 360;
const minDetailsWidth = 240;
const minListWidth = 320;

// Makes `bar`, the Splitter between the commit list and the details panel in `body`, resize the panel. The panel starts
// at `savedWidth` (the default when it's null), and `dotNet.OnDetailsWidthChanged` is told the width the user chooses.
export function attachSplitter(body, bar, savedWidth, dotNet) {
  const details = bar.nextElementSibling;
  return attachSplitterBar({
    bar,
    vertical: true,
    reverse: true,
    getSize: () => details.offsetWidth,
    min: () => minDetailsWidth,
    max: () => Math.max(minDetailsWidth, body.clientWidth - minListWidth - bar.offsetWidth),
    defaultSize: () => defaultDetailsWidth,
    apply: (width) => body.style.setProperty("--git-graph-details-width", `${width}px`),
    observe: body,
    saved: savedWidth,
    save: (width) => dotNet.invokeMethodAsync("OnDetailsWidthChanged", width).catch(() => {
      // The view has gone: the width is lost, which only means the panel opens at its old width next time.
    }),
  });
}
