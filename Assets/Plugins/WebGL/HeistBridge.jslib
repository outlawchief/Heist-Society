mergeInto(LibraryManager.library, {
  HeistNotifyComplete: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    if (typeof window.onHeistComplete === "function") {
      window.onHeistComplete(json);
    }
  },
  HeistNotifyJoinCode: function (codePtr) {
    var code = UTF8ToString(codePtr);
    if (typeof window.onHeistJoinCode === "function") {
      window.onHeistJoinCode(code);
    }
  }
});
