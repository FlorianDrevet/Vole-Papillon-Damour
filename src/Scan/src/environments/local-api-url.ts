export function localApiUrlForHost(hostname: string): string {
  const host = hostname.trim() || 'localhost';
  return `http://${host}:5257`;
}
