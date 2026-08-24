import { VisitedLandmarkDto } from "./dto/aiReport.dto";
import { Landmark } from "../landmark/landmark.interface";
export declare class LandmarkRecommendationService {
    selectRecommendation(visitedLandmarks: VisitedLandmarkDto[], allLandmarks: Landmark[]): Landmark | undefined;
}
