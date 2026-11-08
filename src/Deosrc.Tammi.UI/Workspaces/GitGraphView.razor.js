// The git graph's commit list. Up and Down select the next or previous commit (in .NET), so they mustn't also scroll the
// list; the list scrolls to keep the selected commit in view instead.

export function attach(list) {
  const onKeyDown = (e) => {
    if (e.key === "ArrowUp" || e.key === "ArrowDown") {
      e.preventDefault();
    }
  };

  list.addEventListener("keydown", onKeyDown);
  return {
    // Scrolls the row at `index` into view, if it isn't already. Every row is `rowHeight` pixels high.
    reveal(index, rowHeight) {
      const top = index * rowHeight;
      const bottom = top + rowHeight;
      if (top < list.scrollTop) {
        list.scrollTop = top;
      } else if (bottom > list.scrollTop + list.clientHeight) {
        list.scrollTop = bottom - list.clientHeight;
      }
    },

    scrollToTop() {
      list.scrollTop = 0;
    },

    dispose() {
      list.removeEventListener("keydown", onKeyDown);
    },
  };
}

// The details panel's width, shared by every git graph while the app runs, so a new graph opens at the width the user
// last chose.
const defaultDetailsWidth = 360;
const minDetailsWidth = 240;
const minListWidth = 320;
const keyboardStep = 16;
let detailsWidth = defaultDetailsWidth;

// Makes `splitter`, between the commit list and the details panel in `body`, resize the panel: drag it, or focus it and
// use Left and Right. Double-clicking it goes back to the default width.
export function attachSplitter(body, splitter) {
  const apply = (width) => {
    const max = Math.max(minDetailsWidth, body.clientWidth - minListWidth - splitter.offsetWidth);
    detailsWidth = Math.round(Math.min(Math.max(width, minDetailsWidth), max));
    body.style.setProperty("--git-graph-details-width", `${detailsWidth}px`);
    splitter.setAttribute("aria-valuenow", String(detailsWidth));
    splitter.setAttribute("aria-valuemin", String(minDetailsWidth));
    splitter.setAttribute("aria-valuemax", String(max));
  };

  const onPointerMove = (e) => {
    // The panel ends at the body's right edge, so its width is the distance from the pointer to that edge.
    apply(body.getBoundingClientRect().right - e.clientX - splitter.offsetWidth / 2);
  };

  const onPointerUp = (e) => {
    splitter.releasePointerCapture(e.pointerId);
    splitter.classList.remove("dragging");
    splitter.removeEventListener("pointermove", onPointerMove);
    splitter.removeEventListener("pointerup", onPointerUp);
    splitter.removeEventListener("pointercancel", onPointerUp);
  };

  const onPointerDown = (e) => {
    if (e.button !== 0) {
      return;
    }

    e.preventDefault();
    splitter.setPointerCapture(e.pointerId);
    splitter.classList.add("dragging");
    splitter.addEventListener("pointermove", onPointerMove);
    splitter.addEventListener("pointerup", onPointerUp);
    splitter.addEventListener("pointercancel", onPointerUp);
  };

  const onKeyDown = (e) => {
    if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
      e.preventDefault();
      apply(detailsWidth + (e.key === "ArrowLeft" ? keyboardStep : -keyboardStep));
    }
  };

  const onDoubleClick = () => apply(defaultDetailsWidth);

  // Keep the panel within its limits as the window changes size.
  const observer = new ResizeObserver(() => apply(detailsWidth));

  splitter.addEventListener("pointerdown", onPointerDown);
  splitter.addEventListener("keydown", onKeyDown);
  splitter.addEventListener("dblclick", onDoubleClick);
  observer.observe(body);
  apply(detailsWidth);

  return {
    dispose() {
      observer.disconnect();
      splitter.removeEventListener("pointerdown", onPointerDown);
      splitter.removeEventListener("keydown", onKeyDown);
      splitter.removeEventListener("dblclick", onDoubleClick);
      splitter.removeEventListener("pointermove", onPointerMove);
      splitter.removeEventListener("pointerup", onPointerUp);
      splitter.removeEventListener("pointercancel", onPointerUp);
    },
  };
}
