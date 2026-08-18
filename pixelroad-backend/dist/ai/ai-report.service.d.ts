import { LandmarkService } from "../landmark/landmark.service";
import { VisitedLandmarkDto } from "./dto/aiReport.dto";
import { Landmark } from "../landmark/landmark.interface";
export declare class AiReportService {
    private readonly landmarkService;
    constructor(landmarkService: LandmarkService);
    analysis(visitedLandmarks: VisitedLandmarkDto[]): Promise<Landmark[]>;
}
