// The commit panel for a git graph's working changes.

import { attach } from "../Components/Splitter.razor.js";

// Makes `bar`, the Splitter above the commit message in the panel `root`, resize the message's body `body`: drag it up
// to make the body taller and down to make it shorter. The body always shows at least one row, and the files above
// keep some room.
//
// The body starts at `savedHeight`, the height the user chose in pixels (its natural height when it's null), and
// `dotNet.OnMessageHeightChanged` is told the height the user chooses.
export function attachSplitter(root, body, bar, savedHeight, dotNet) {
  const frameHeight = () => {
    const style = getComputedStyle(body);
    return parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
      + parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
  };

  const rowHeight = () => {
    const style = getComputedStyle(body);
    return parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4;
  };

  const min = () => Math.ceil(rowHeight() + frameHeight());

  // Everything else in the panel keeps its height, and each file area keeps at least its minimum.
  const max = () => {
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
    return Math.max(min(), Math.floor(root.clientHeight - others));
  };

  return attach({
    bar,
    vertical: false,
    reverse: true,
    getSize: () => body.offsetHeight,
    min,
    max,
    defaultSize: () => null,
    apply: (height) => {
      if (height === null) {
        body.style.removeProperty("height");
      } else {
        body.style.height = `${height}px`;
      }
    },
    observe: root,
    saved: savedHeight,
    save: (height) => dotNet.invokeMethodAsync("OnMessageHeightChanged", height).catch(() => {
      // The panel has gone: the height is lost, which only means the body opens at its old height next time.
    }),
  });
}
