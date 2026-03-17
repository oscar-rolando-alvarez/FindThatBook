export interface BookSearchRequest {
  query: string;
  maxResults?: number;
  enableAiReranking?: boolean;
}

export interface ExtractionDto {
  title?: string;
  author?: string;
  keywords: string[];
  year?: number;
  confidence: 'high' | 'medium' | 'low';
  method: string;
  reasoning: string;
}

export interface BookResultDto {
  title: string;
  author?: string;
  allAuthors: string[];
  firstPublishYear?: number;
  openLibraryWorkId: string;
  openLibraryUrl: string;
  coverImageUrl?: string;
  matchRank: number;
  matchType: string;
  explanation: string;
}

export interface BookSearchResponse {
  query: string;
  extraction: ExtractionDto;
  results: BookResultDto[];
  totalCandidates: number;
  processingTimeMs: number;
}
