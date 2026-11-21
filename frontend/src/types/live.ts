export interface SlideContent {
  id: string;
  title: string;
  text: string;
  reference?: string;
}

export interface LiveFlags {
  clearText: boolean;
  blackout: boolean;
  logo: boolean;
}

export interface LiveState {
  currentSlide: SlideContent | null;
  flags: LiveFlags;
}
