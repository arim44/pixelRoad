import { CreateReportDto } from './dto/aiReport.dto';
import { AiReportService } from './ai-report.service';
import { LandmarkService } from '../landmark/landmark.service';
export declare class AiService {
    private readonly aireportService;
    private readonly landmarkService;
    constructor(aireportService: AiReportService, landmarkService: LandmarkService);
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
