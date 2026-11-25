// The selected commit's details beside a git graph.

import { attach } from "../Components/Splitter.razor.js";

// The body's default height, in lines, and the least it shows. The rest of the panel keeps at least `minLowerHeight`.
const defaultLines = 8;
const minLines = 3;
const minLowerHeight = 120;

// Makes `bar`, the Splitter below the message's body `message` in the details `root`, resize the body. The body starts
// at `savedHeight` (the default when it's null), and `dotNet.OnMessageHeightChanged` is told the height the user
// chooses. When the panel is too short for it, the body is shown shorter (but never less than three lines).
export function attachMessageSplitter(root, message, bar, savedHeight, dotNet) {
  const linesHeight = (lines) => {
    const style = getComputedStyle(message);
    const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4;
    const frame = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
      + parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
    return Math.ceil(lines * lineHeight + frame);
  };

  const min = () => linesHeight(minLines);
  const script = attach({
    bar,
    vertical: false,
    reverse: false,
    getSize: () => message.offsetHeight,
    min,
    max: () => {
      const room = root.getBoundingClientRect().bottom - message.getBoundingClientRect().top
        - bar.offsetHeight - minLowerHeight;
      return Math.max(min(), Math.floor(room));
    },
    defaultSize: () => linesHeight(defaultLines),
    apply: (height) => root.style.setProperty("--git-commit-body-height", `${height}px`),
    observe: root,
    saved: savedHeight,
    save: (height) => dotNet.invokeMethodAsync("OnMessageHeightChanged", height).catch(() => {
      // The panel has gone: the height is lost, which only means the body opens at its old height next time.
    }),
  });

  return {
    dispose() {
      script.dispose();
      root.style.removeProperty("--git-commit-body-height");
    },
  };
}
