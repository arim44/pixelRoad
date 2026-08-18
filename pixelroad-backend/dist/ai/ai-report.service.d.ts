import { LandmarkService } from "../landmark/landmark.service";
import { VisitedLandmarkDto } from "./dto/aiReport.dto";
import { LandmarkRecommendationService } from "./landmark-recommendation.service";
export declare class AiReportService {
    private readonly landmarkService;
    private readonly recommendationService;
    constructor(landmarkService: LandmarkService, recommendationService: LandmarkRecommendationService);
    getReportData(visitedLandmarks: VisitedLandmarkDto[]): Promise<{
        recommendation: undefined;
        systemPrompt: string;
        prompt: string;
    } | {
        recommendation: import("../landmark/landmark.interface").Landmark;
        systemPrompt: string;
        prompt: string;
    }>;
}
