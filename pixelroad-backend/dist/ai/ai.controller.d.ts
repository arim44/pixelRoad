import { AiService } from './ai.service';
import { CreateReportDto } from './dto/aiReport.dto';
export declare class AiController {
    private readonly aiService;
    constructor(aiService: AiService);
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
