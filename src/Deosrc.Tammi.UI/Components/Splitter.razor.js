// Makes a Splitter bar resize one part of a panel: drag it, or focus it and use the arrow keys, which grow the part
// when they point the way its far edge is. Double-clicking it goes back to the part's default size.
//
// The part's size is the one the user chose, in pixels (null for the default). When the panel has no room for it the
// part is shown smaller, and goes back to the chosen size when there's room: only the user's resizing changes it, and
// then `save` is told the new size (null for the default), to keep it for next time.
//
// The options are:
//   bar         the Splitter's element.
//   vertical    whether the bar is a vertical line, so the part's width is resized rather than its height.
//   reverse     whether the part is after the bar, so dragging towards the bar grows it.
//   getSize()   the part's size now, in pixels.
//   min(), max()  the least and most the part can be, in pixels, with the panel as it is now.
//   defaultSize() the part's default size, in pixels, or null to leave it at its natural size.
//   apply(size) shows the part at `size` pixels, or at its natural size for null.
//   observe     the element whose size changes may leave the part without room for its size.
//   saved       the size the user chose last time, or null.
//   save(size)  called with the size the user chose.

const keyboardStep = 16;

export function attach({ bar, vertical, reverse, getSize, min, max, defaultSize, apply, observe, saved, save }) {
  let chosen = saved ?? null;
  const sign = reverse ? -1 : 1;
  const position = (e) => (vertical ? e.clientX : e.clientY);
  const grows = vertical ? (reverse ? "ArrowLeft" : "ArrowRight") : (reverse ? "ArrowUp" : "ArrowDown");
  const shrinks = vertical ? (reverse ? "ArrowRight" : "ArrowLeft") : (reverse ? "ArrowDown" : "ArrowUp");

  const limit = (size) => Math.round(Math.min(Math.max(size, min()), max()));

  // Shows the chosen size, as far as the panel has room for it.
  const show = () => {
    const wanted = chosen ?? defaultSize();
    apply(wanted === null ? null : limit(wanted));
    bar.setAttribute("aria-valuenow", String(Math.round(getSize())));
    bar.setAttribute("aria-valuemin", String(min()));
    bar.setAttribute("aria-valuemax", String(max()));
  };

  // The user chose a size: it's limited to what fits now, which is what they see.
  const choose = (size) => {
    chosen = limit(size);
    show();
  };

  let dragStart = null;

  const onPointerMove = (e) => choose(dragStart.size + sign * (position(e) - dragStart.position));

  const onPointerUp = (e) => {
    bar.releasePointerCapture(e.pointerId);
    bar.classList.remove("dragging");
    bar.removeEventListener("pointermove", onPointerMove);
    bar.removeEventListener("pointerup", onPointerUp);
    bar.removeEventListener("pointercancel", onPointerUp);
    if (chosen !== dragStart.chosen) {
      save(chosen);
    }
  };

  const onPointerDown = (e) => {
    if (e.button !== 0) {
      return;
    }

    e.preventDefault();
    dragStart = { position: position(e), size: getSize(), chosen };
    bar.setPointerCapture(e.pointerId);
    bar.classList.add("dragging");
    bar.addEventListener("pointermove", onPointerMove);
    bar.addEventListener("pointerup", onPointerUp);
    bar.addEventListener("pointercancel", onPointerUp);
  };

  // Keys move from the size shown. A held key repeats its keydown but has one keyup, so the size is saved once the
  // key is let go, not at every step.
  let keyStart = null;

  const onKeyDown = (e) => {
    if (e.key !== grows && e.key !== shrinks) {
      return;
    }

    e.preventDefault();
    keyStart ??= { chosen };
    choose(getSize() + (e.key === grows ? keyboardStep : -keyboardStep));
  };

  const onKeyUp = (e) => {
    if ((e.key === grows || e.key === shrinks) && keyStart !== null) {
      if (chosen !== keyStart.chosen) {
        save(chosen);
      }

      keyStart = null;
    }
  };

  const onDoubleClick = () => {
    if (chosen !== null) {
      chosen = null;
      show();
      save(null);
    }
  };

  const observer = new ResizeObserver(show);

  bar.addEventListener("pointerdown", onPointerDown);
  bar.addEventListener("keydown", onKeyDown);
  bar.addEventListener("keyup", onKeyUp);
  bar.addEventListener("dblclick", onDoubleClick);
  observer.observe(observe);
  show();

  return {
    dispose() {
      observer.disconnect();
      bar.removeEventListener("pointerdown", onPointerDown);
      bar.removeEventListener("keydown", onKeyDown);
      bar.removeEventListener("keyup", onKeyUp);
      bar.removeEventListener("dblclick", onDoubleClick);
      bar.removeEventListener("pointermove", onPointerMove);
      bar.removeEventListener("pointerup", onPointerUp);
      bar.removeEventListener("pointercancel", onPointerUp);
    },
  };
}
