export function isEmailNotVerifiedMessage(message: string | undefined): boolean {
  return message?.toLowerCase().includes('not verified') ?? false;
}
