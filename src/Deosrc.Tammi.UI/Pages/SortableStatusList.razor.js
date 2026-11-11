// Drag and drop for the status order on Home. Items (`li`) are dragged from anywhere but their buttons. Only classes are
// changed here, so Blazor doesn't need to render while the drag is in progress. When an item is dropped,
// `dotNet.OnDropped(from, to)` is told the indexes.
export function attach(list, dotNet) {
  let from = -1;
  let startedOnButton = false;

  const items = () => [...list.children];
  const clear = () => {
    for (const item of items()) {
      item.classList.remove("dragging", "drag-over");
    }
  };

  const onPointerDown = (e) => {
    startedOnButton = e.target.closest("button") !== null;
  };

  const onDragStart = (e) => {
    const item = e.target.closest("li");
    if (startedOnButton || item === null || item.parentElement !== list) {
      e.preventDefault();
      return;
    }

    from = items().indexOf(item);
    // Some web views only start a drag when data is set.
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", String(from));
    // Later, so the browser captures the item's normal look as the drag image.
    setTimeout(() => item.classList.add("dragging"), 0);
  };

  const onDragOver = (e) => {
    if (from < 0) {
      return;
    }

    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    const over = e.target.closest("li");
    for (const item of items()) {
      item.classList.toggle("drag-over", item === over && items().indexOf(item) !== from);
    }
  };

  const onDrop = (e) => {
    if (from < 0) {
      return;
    }

    e.preventDefault();
    const target = items().indexOf(e.target.closest("li"));
    const source = from;
    from = -1;
    clear();
    if (target >= 0 && target !== source) {
      dotNet.invokeMethodAsync("OnDropped", source, target);
    }
  };

  const onDragEnd = () => {
    from = -1;
    clear();
  };

  list.addEventListener("pointerdown", onPointerDown);
  list.addEventListener("dragstart", onDragStart);
  list.addEventListener("dragover", onDragOver);
  list.addEventListener("drop", onDrop);
  list.addEventListener("dragend", onDragEnd);

  return {
    dispose() {
      list.removeEventListener("pointerdown", onPointerDown);
      list.removeEventListener("dragstart", onDragStart);
      list.removeEventListener("dragover", onDragOver);
      list.removeEventListener("drop", onDrop);
      list.removeEventListener("dragend", onDragEnd);
    },
  };
}
