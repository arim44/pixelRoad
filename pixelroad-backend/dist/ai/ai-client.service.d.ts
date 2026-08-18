interface AiReportResult {
    analysis: string;
    reason: string;
}
export declare class AiClientService {
    private readonly client;
    constructor();
    generateReport(userPrompt: string): Promise<AiReportResult>;
}
export {};
