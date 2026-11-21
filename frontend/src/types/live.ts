export type EffectiveDisplayMode = 'Idle' | 'Slide' | 'ClearText' | 'Logo' | 'Blackout';
export type TextAlignment = 'Left' | 'Center' | 'Right';
export type LowerThirdPosition = 'Bottom' | 'Middle' | 'Top';

export interface ProjectorTheme {
  fontFamily: string;
  fontSizePx: number;
  textColor: string;
  backgroundColor: string;
  alignment: TextAlignment;
}

export interface ObsTheme {
  fontFamily: string;
  fontSizePx: number;
  textColor: string;
  boxBackgroundColor: string;
  position: LowerThirdPosition;
  marginHorizontalPx: number;
  marginVerticalPx: number;
  borderRadiusPx: number;
}

export interface ThemeSettings {
  projector: ProjectorTheme;
  obs: ObsTheme;
}

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
  theme?: ThemeSettings;
  effectiveTheme?: ThemeSettings;
}
