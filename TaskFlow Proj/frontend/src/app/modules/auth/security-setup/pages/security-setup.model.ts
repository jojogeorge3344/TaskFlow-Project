export type SetupStep = 'details' | 'authenticator' | 'codes';

export interface StepInfo {
  id: SetupStep;
  title: string;
  hint: string;
}
