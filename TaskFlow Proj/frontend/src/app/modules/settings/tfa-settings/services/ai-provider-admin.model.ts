/** The document index TFA answers from, as the Document search card shows it. */
export interface DocumentIndexStatus {
  enabled: boolean;
  sourcePath: string;
  folderFound: boolean;
  embeddingModel: string;
  fileCount: number;
  chunkCount: number;
  lastIndexedAt: string | null;
  isRunning: boolean;
  lastError: string | null;
  lastErrorFile: string | null;
}
