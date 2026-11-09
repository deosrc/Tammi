// The commit panel for a git graph's working changes.

const keyboardStep = 16;

// Makes `splitter`, above the commit message in the panel `root`, resize the message's body `body`: drag it up to make
// the body taller and down to make it shorter, or focus it and use Up and Down. Double-clicking it goes back to the
// default. The body always shows at least one row, and the files above keep some room.
//
// The body starts at `savedHeight`, the height the user chose in pixels (the default when it's null). When the panel is
// too short for it, the body is shown shorter, and goes back to it when there's room. Only the user's resizing changes
// it, and then `dotNet.OnMessageHeightChanged` is told the new height (null for the default), to keep it for next time.
export function attachSplitter(root, body, splitter, savedHeight, dotNet) {
  let chosenHeight = savedHeight ?? null;

  const frameHeight = () => {
    const style = getComputedStyle(body);
    return parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
      + parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
  };

  const rowHeight = () => {
    const style = getComputedStyle(body);
    return parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4;
  };

  const minHeight = () => Math.ceil(rowHeight() + frameHeight());

  // Everything else in the panel keeps its height, and each file area keeps at least its minimum.
  const maxHeight = () => {
    const panel = getComputedStyle(root);
    const gap = parseFloat(panel.rowGap) || 0;
    let others = parseFloat(panel.paddingTop) + parseFloat(panel.paddingBottom);
    let count = 0;
    for (const child of root.children) {
      count++;
      if (child.classList.contains("git-working-section")) {
        others += parseFloat(getComputedStyle(child).minHeight) || 0;
      } else if (child.classList.contains("git-working-message")) {
        others += child.offsetHeight - body.offsetHeight;
      } else {
        others += child.offsetHeight;
      }
    }

    others += gap * Math.max(count - 1, 0);
    return Math.max(minHeight(), Math.floor(root.clientHeight - others));
  };

  const show = () => {
    if (chosenHeight === null) {
      body.style.removeProperty("height");
    } else {
      const shown = Math.round(Math.min(Math.max(chosenHeight, minHeight()), maxHeight()));
      body.style.height = `${shown}px`;
    }

    splitter.setAttribute("aria-valuenow", String(Math.round(body.offsetHeight)));
    splitter.setAttribute("aria-valuemin", String(minHeight()));
    splitter.setAttribute("aria-valuemax", String(maxHeight()));
  };

  const choose = (height) => {
    chosenHeight = Math.round(Math.min(Math.max(height, minHeight()), maxHeight()));
    show();
  };

  const save = () => {
    dotNet.invokeMethodAsync("OnMessageHeightChanged", chosenHeight).catch(() => {
      // The panel has gone: the height is lost, which only means the body opens at its old height next time.
    });
  };

  let dragStartY = 0;
  let dragStartHeight = 0;
  let dragStartChosen = null;

  const onPointerMove = (e) => choose(dragStartHeight + (dragStartY - e.clientY));

  const onPointerUp = (e) => {
    splitter.releasePointerCapture(e.pointerId);
    splitter.classList.remove("dragging");
    splitter.removeEventListener("pointermove", onPointerMove);
    splitter.removeEventListener("pointerup", onPointerUp);
    splitter.removeEventListener("pointercancel", onPointerUp);
    if (chosenHeight !== dragStartChosen) {
      save();
    }
  };

  const onPointerDown = (e) => {
    if (e.button !== 0) {
      return;
    }

    e.preventDefault();
    dragStartY = e.clientY;
    dragStartHeight = body.offsetHeight;
    dragStartChosen = chosenHeight;
    splitter.setPointerCapture(e.pointerId);
    splitter.classList.add("dragging");
    splitter.addEventListener("pointermove", onPointerMove);
    splitter.addEventListener("pointerup", onPointerUp);
    splitter.addEventListener("pointercancel", onPointerUp);
  };

  let keyStartChosen;

  const onKeyDown = (e) => {
    if (e.key !== "ArrowUp" && e.key !== "ArrowDown") {
      return;
    }

    e.preventDefault();
    keyStartChosen ??= chosenHeight;
    choose(body.offsetHeight + (e.key === "ArrowUp" ? keyboardStep : -keyboardStep));
  };

  // A held key is saved once, when it's let go.
  const onKeyUp = (e) => {
    if ((e.key === "ArrowUp" || e.key === "ArrowDown") && keyStartChosen !== undefined) {
      if (chosenHeight !== keyStartChosen) {
        save();
      }

      keyStartChosen = undefined;
    }
  };

  const onDoubleClick = () => {
    if (chosenHeight !== null) {
      chosenHeight = null;
      show();
      save();
    }
  };

  // A panel that gets shorter or taller may no longer have room for the chosen height.
  const observer = new ResizeObserver(show);
  observer.observe(root);

  splitter.addEventListener("pointerdown", onPointerDown);
  splitter.addEventListener("keydown", onKeyDown);
  splitter.addEventListener("keyup", onKeyUp);
  splitter.addEventListener("dblclick", onDoubleClick);
  show();

  return {
    dispose() {
      observer.disconnect();
      splitter.removeEventListener("pointerdown", onPointerDown);
      splitter.removeEventListener("keydown", onKeyDown);
      splitter.removeEventListener("keyup", onKeyUp);
      splitter.removeEventListener("dblclick", onDoubleClick);
      splitter.removeEventListener("pointermove", onPointerMove);
      splitter.removeEventListener("pointerup", onPointerUp);
      splitter.removeEventListener("pointercancel", onPointerUp);
    },
  };
}
