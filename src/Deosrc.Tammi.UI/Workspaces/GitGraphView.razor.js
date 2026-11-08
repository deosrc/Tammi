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
