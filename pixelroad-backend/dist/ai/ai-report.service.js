"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.AiReportService = void 0;
const common_1 = require("@nestjs/common");
const landmark_service_1 = require("../landmark/landmark.service");
const landmark_recommendation_service_1 = require("./landmark-recommendation.service");
const ai_report_prompt_constant_1 = require("./constants/ai-report-prompt.constant");
let AiReportService = class AiReportService {
    landmarkService;
    recommendationService;
    constructor(landmarkService, recommendationService) {
        this.landmarkService = landmarkService;
        this.recommendationService = recommendationService;
    }
    async getReportData(visitedLandmarks) {
        const visited = [];
        for (const visitedLandmark of visitedLandmarks) {
            const landmark = await this.landmarkService.getLandmarkById(visitedLandmark.landmarkId);
            if (landmark) {
                visited.push({
                    landmarkId: landmark.id,
                    name: landmark.name,
                    category: landmark.category,
                    visitCount: visitedLandmark.visitCount,
                });
            }
        }
        const allLandmarks = await this.landmarkService.getAllLandmarks();
        const recommendation = this.recommendationService.selectRecommendation(visitedLandmarks, allLandmarks);
        const prompt = (0, ai_report_prompt_constant_1.createAiReportPrompt)(visited, recommendation);
        return {
            recommendation,
            systemPrompt: ai_report_prompt_constant_1.AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS,
            prompt,
        };
    }
};
exports.AiReportService = AiReportService;
exports.AiReportService = AiReportService = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [landmark_service_1.LandmarkService,
        landmark_recommendation_service_1.LandmarkRecommendationService])
], AiReportService);
//# sourceMappingURL=ai-report.service.js.map