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
exports.AiService = void 0;
const common_1 = require("@nestjs/common");
const ai_report_service_1 = require("./ai-report.service");
const ai_client_service_1 = require("./ai-client.service");
let AiService = class AiService {
    aireportService;
    aiClientService;
    constructor(aireportService, aiClientService) {
        this.aireportService = aireportService;
        this.aiClientService = aiClientService;
    }
    async createReport(dto) {
        const result = await this.aireportService.getReportData(dto.visitedLandmarks);
        const aiResult = await this.aiClientService.generateReport(result.prompt);
        return {
            success: true,
            data: {
                analysis: aiResult.analysis,
                recommendation: result.recommendation ? {
                    landmarkId: result.recommendation?.id ?? 0,
                    name: result.recommendation?.name ?? '',
                    reason: aiResult.reason,
                } : {
                    landmarkId: 0,
                    name: "",
                    reason: "현재 추천 가능한 랜드마크가 없습니다."
                }
            }
        };
    }
};
exports.AiService = AiService;
exports.AiService = AiService = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [ai_report_service_1.AiReportService,
        ai_client_service_1.AiClientService])
], AiService);
//# sourceMappingURL=ai.service.js.map