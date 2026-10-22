import { DOCUMENT, Injectable, inject } from '@angular/core';

/**
 * Hands a file the application already holds in memory (a `Blob` fetched with `HttpClient`) to the browser as a download.
 * Isolated in a service so that it can be replaced in tests, as {@link BrowserNavigation} is. The file never leaves the
 * browser: the object URL is local to the page and is released as soon as the download has started.
 */
@Injectable({ providedIn: 'root' })
export class FileDownload {
  private readonly document = inject(DOCUMENT);

  save(content: Blob, fileName: string): void {
    const url = URL.createObjectURL(content);
    const link = this.document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.hidden = true;
    this.document.body.appendChild(link);
    link.click();
    link.remove();
    // Releasing the URL in the same turn can cancel the download in some browsers.
    setTimeout(() => URL.revokeObjectURL(url));
  }
}
