import { TestBed } from '@angular/core/testing';
import { FileDownload } from './file-download';

describe('FileDownload', () => {
  let service: FileDownload;
  let create: ReturnType<typeof vi.fn>;
  let revoke: ReturnType<typeof vi.fn>;
  let clicked: HTMLAnchorElement[];
  // jsdom does not implement object URLs: put the doubles on `URL` and give it back the way it was.
  const original = { create: URL.createObjectURL, revoke: URL.revokeObjectURL };

  beforeEach(() => {
    vi.useFakeTimers();
    create = vi.fn().mockReturnValue('blob:itmas/file-1');
    revoke = vi.fn();
    URL.createObjectURL = create as typeof URL.createObjectURL;
    URL.revokeObjectURL = revoke as typeof URL.revokeObjectURL;
    clicked = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      // Capture the state of the link at the moment it is pressed: it is removed right after.
      clicked.push(this.cloneNode() as HTMLAnchorElement);
      expect(document.body.contains(this)).toBe(true);
    });
    service = TestBed.inject(FileDownload);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    URL.createObjectURL = original.create;
    URL.revokeObjectURL = original.revoke;
  });

  it('presses a hidden link to the object URL of the file, with the name given as the download name', () => {
    const file = new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' });

    service.save(file, 'agenda-2026.ics');

    expect(create).toHaveBeenCalledWith(file);
    expect(clicked).toHaveLength(1);
    expect(clicked[0].getAttribute('href')).toBe('blob:itmas/file-1');
    expect(clicked[0].getAttribute('download')).toBe('agenda-2026.ics');
    expect(clicked[0].hidden).toBe(true);
  });

  it('removes the link from the page after pressing it', () => {
    service.save(new Blob(['x']), 'agenda-2026.ics');

    expect(document.body.querySelector('a[download]')).toBeNull();
  });

  it('releases the object URL after the download started, not before', () => {
    service.save(new Blob(['x']), 'agenda-2026.ics');

    expect(revoke).not.toHaveBeenCalled();
    vi.runAllTimers();
    expect(revoke).toHaveBeenCalledWith('blob:itmas/file-1');
  });
});
