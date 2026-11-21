import type { LiveFlags, LiveState, SlideContent } from '../../src/types/live';

export class SlideContentBuilder {
  private id = 'slide-1';
  private title = 'Canto 1';
  private text = 'Alabado sea su nombre';
  private reference: string | undefined;

  withId(id: string): this {
    this.id = id;
    return this;
  }

  withTitle(title: string): this {
    this.title = title;
    return this;
  }

  withText(text: string): this {
    this.text = text;
    return this;
  }

  withReference(reference?: string): this {
    this.reference = reference;
    return this;
  }

  build(): SlideContent {
    return {
      id: this.id,
      title: this.title,
      text: this.text,
      ...(this.reference ? { reference: this.reference } : {}),
    };
  }
}

export class LiveStateBuilder {
  private currentSlide: SlideContent | null = null;
  private flags: LiveFlags = {
    clearText: false,
    blackout: false,
    logo: false,
  };

  withSlide(slide: SlideContent | null): this {
    this.currentSlide = slide;
    return this;
  }

  withFlags(flags: Partial<LiveFlags>): this {
    this.flags = { ...this.flags, ...flags };
    return this;
  }

  build(): LiveState {
    return {
      currentSlide: this.currentSlide,
      flags: { ...this.flags },
    };
  }
}
