// Lets the user resize a .data-table's columns by dragging the handles in its headers, or with the arrow keys when a
// handle has focus. A column's width goes in a --column-<index> custom property on the table, which its grid uses
// instead of the column's natural width. Double-clicking a handle, or pressing Enter on it, fits the column again.

const MIN_WIDTH = 48;
const KEY_STEP = 16;
const handlers = new WeakMap();

function setWidth(table, column, width) {
  // minmax(0, …) lets the column still shrink, with an ellipsis, when the page is narrower than the table.
  table.style.setProperty(`--column-${column}`, `minmax(0, ${Math.max(MIN_WIDTH, Math.round(width))}px)`);
}

function fit(table, column) {
  table.style.removeProperty(`--column-${column}`);
}

function headerWidth(handle) {
  return handle.closest('th').getBoundingClientRect().width;
}

export function attach(table) {
  if (!table || handlers.has(table)) {
    return;
  }

  const onPointerDown = event => {
    const handle = event.target.closest('.data-table-resize');
    if (!handle || event.button !== 0) {
      return;
    }

    event.preventDefault();
    const column = handle.dataset.column;
    const startX = event.clientX;
    const startWidth = headerWidth(handle);
    handle.setPointerCapture(event.pointerId);
    table.classList.add('data-table-resizing');

    const onMove = moveEvent => setWidth(table, column, startWidth + moveEvent.clientX - startX);
    const onUp = () => {
      handle.removeEventListener('pointermove', onMove);
      handle.removeEventListener('pointerup', onUp);
      handle.removeEventListener('pointercancel', onUp);
      table.classList.remove('data-table-resizing');
    };
    handle.addEventListener('pointermove', onMove);
    handle.addEventListener('pointerup', onUp);
    handle.addEventListener('pointercancel', onUp);
  };

  const onDoubleClick = event => {
    const handle = event.target.closest('.data-table-resize');
    if (handle) {
      fit(table, handle.dataset.column);
    }
  };

  const onKeyDown = event => {
    const handle = event.target.closest('.data-table-resize');
    if (!handle) {
      return;
    }

    const column = handle.dataset.column;
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      setWidth(table, column, headerWidth(handle) + (event.key === 'ArrowRight' ? KEY_STEP : -KEY_STEP));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      fit(table, column);
    }
  };

  table.addEventListener('pointerdown', onPointerDown);
  table.addEventListener('dblclick', onDoubleClick);
  table.addEventListener('keydown', onKeyDown);
  handlers.set(table, { onPointerDown, onDoubleClick, onKeyDown });
}

export function detach(table) {
  const found = table && handlers.get(table);
  if (!found) {
    return;
  }

  table.removeEventListener('pointerdown', found.onPointerDown);
  table.removeEventListener('dblclick', found.onDoubleClick);
  table.removeEventListener('keydown', found.onKeyDown);
  handlers.delete(table);
}
