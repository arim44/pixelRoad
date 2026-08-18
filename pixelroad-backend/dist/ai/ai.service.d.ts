import { CreateReportDto } from './dto/aiReport.dto';
import { AiReportService } from './ai-report.service';
import { AiClientService } from './ai-client.service';
export declare class AiService {
    private readonly aireportService;
    private readonly aiClientService;
    constructor(aireportService: AiReportService, aiClientService: AiClientService);
    createReport(dto: CreateReportDto): Promise<{
        success: boolean;
        data: {
            analysis: string;
            recommendation: {
                landmarkId: number;
                name: string;
                reason: string;
            };
        };
    }>;
}
