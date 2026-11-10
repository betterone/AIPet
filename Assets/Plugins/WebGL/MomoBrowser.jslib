mergeInto(LibraryManager.library, {
  MomoBrowserPrepare: function () {
    var canvas = Module.canvas;
    if (!canvas || canvas.__momoPointerReady) return;
    canvas.__momoPointerReady = true;
    // Right drag is the desktop look gesture, not the browser context-menu gesture.
    canvas.addEventListener('contextmenu', function (event) { event.preventDefault(); });
  },
  MomoBrowserDownloadText: function (name, contents) {
    var blob = new Blob([UTF8ToString(contents)], { type: 'application/json;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = UTF8ToString(name);
    link.click();
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  },
  MomoBrowserDownloadBytes: function (name, bytes, length) {
    var data = HEAPU8.slice(bytes, bytes + length);
    var blob = new Blob([data], { type: 'image/png' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = UTF8ToString(name);
    link.click();
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  }
});
