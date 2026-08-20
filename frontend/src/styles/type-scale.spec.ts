/**
 * Guards design.md §3.1's type scale against the implementation.
 *
 * This exists because the table and the theme config silently disagreed: `mat.theme()`
 * exposes only three weight knobs (regular/medium/bold) and Angular Material maps
 * display-*, headline-* and title-large onto *regular*, which §3.1 sets to 300. Five
 * roles therefore emitted 300 instead of the weight §3.1 asks for, with no build error
 * and no test to catch it — a `mat-card-title` rendered lighter than its own subtitle.
 *
 * `styles.scss` is loaded into the Karma bundle (angular.json → test.options.styles),
 * so the emitted custom properties are readable straight off <html> here.
 */
describe('design.md §3.1 type scale', () => {
  /** role → [weight, size px, line-height px] exactly as §3.1's table states them. */
  const SCALE: Record<string, [string, number, number]> = {
    'display-large': ['300', 57, 64],
    'display-medium': ['300', 45, 52],
    'display-small': ['400', 36, 44],
    'headline-large': ['600', 32, 40],
    'headline-medium': ['600', 28, 36],
    'headline-small': ['600', 24, 32],
    'title-large': ['500', 22, 28],
    'title-medium': ['500', 16, 24],
    'title-small': ['500', 14, 20],
    'body-large': ['300', 16, 24],
    'body-medium': ['300', 14, 20],
    'body-small': ['300', 12, 16],
    'label-large': ['500', 14, 20],
    'label-medium': ['500', 12, 16],
    'label-small': ['500', 11, 16]
  };

  /** Resolves a `font:` shorthand through a real element, so var() substitution runs. */
  function computeFont(role: string): CSSStyleDeclaration {
    const probe = document.createElement('span');
    probe.style.font = `var(--mat-sys-${role})`;
    document.body.appendChild(probe);
    const computed = getComputedStyle(probe);
    // Read every value before the element leaves the document.
    const snapshot = {
      fontWeight: computed.fontWeight,
      fontSize: computed.fontSize,
      lineHeight: computed.lineHeight,
      fontFamily: computed.fontFamily
    } as CSSStyleDeclaration;
    probe.remove();
    return snapshot;
  }

  function subVariable(role: string, part: string): string {
    return getComputedStyle(document.documentElement)
      .getPropertyValue(`--mat-sys-${role}-${part}`)
      .trim();
  }

  for (const [role, [weight, sizePx, lineHeightPx]] of Object.entries(SCALE)) {
    describe(role, () => {
      it(`carries weight ${weight}`, () => {
        expect(subVariable(role, 'weight')).toBe(weight);
      });

      // The five overridden roles rebuild the shorthand from sub-variables; if that
      // composition were invalid at computed-value time the weight would silently fall
      // back to `normal` (400) here while the sub-variable above still looked right.
      it(`resolves its font shorthand to weight ${weight}`, () => {
        expect(computeFont(role).fontWeight).toBe(weight);
      });

      // Sizes are compared with sub-pixel tolerance: mat.theme() emits them in rem
      // rounded to three decimals, so 57px arrives as 3.562rem = 56.992px.
      it(`is ${sizePx}/${lineHeightPx} in Poppins`, () => {
        const font = computeFont(role);
        expect(parseFloat(font.fontSize)).toBeCloseTo(sizePx, 1);
        expect(parseFloat(font.lineHeight)).toBeCloseTo(lineHeightPx, 1);
        expect(font.fontFamily).toContain('Poppins');
      });
    });
  }

  it('never renders a card title lighter than its own subtitle', () => {
    // The concrete symptom that motivated the overrides: title-large (card title)
    // must not be lighter than title-medium (card subtitle).
    const title = Number(subVariable('title-large', 'weight'));
    const subtitle = Number(subVariable('title-medium', 'weight'));
    expect(title).toBeGreaterThanOrEqual(subtitle);
  });
});
