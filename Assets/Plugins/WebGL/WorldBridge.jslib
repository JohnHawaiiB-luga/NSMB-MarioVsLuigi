mergeInto(LibraryManager.library, {
  WorldSetGameplay: function (active) {
    if (typeof window !== "undefined" && window.__worldGameplay) {
      window.__worldGameplay(!!active);
    }
  },

  // A destination is what turns a pipe into a door: the game finishes its quit
  // ritual and the page it lands on is wherever that pipe went.
  WorldExitSite: function (urlPtr) {
    if (typeof window === "undefined" || !window.__worldExit) {
      return;
    }
    window.__worldExit(urlPtr ? UTF8ToString(urlPtr) : "");
  }
});
