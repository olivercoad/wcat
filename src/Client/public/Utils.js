export function OpenInNewTab(src, title) {
    let ww  = window.open('_blank');
    //opening tabs with data uri is no longer supported in browsers, so embedding
    //the datauri document in iframe in the new tab
    ww.document.write(`
    <!DOCTYPE html>
        <!-- full page iframe https://stackoverflow.com/a/60159248 -->
        <meta charset=utf-8>
        <meta name=viewport content="width=device-width">
        <iframe src=${src} style="position:absolute; top:0; left:0; width:100%; height:100%; border:0">
        </iframe>`);
    ww.document.title = title
}