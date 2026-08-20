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
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.AiController = void 0;
const common_1 = require("@nestjs/common");
const ai_service_1 = require("./ai.service");
const swagger_1 = require("@nestjs/swagger");
const aiReport_dto_1 = require("./dto/aiReport.dto");
let AiController = class AiController {
    aiService;
    constructor(aiService) {
        this.aiService = aiService;
    }
    async createReport(dto) {
        return this.aiService.createReport(dto);
    }
};
exports.AiController = AiController;
__decorate([
    (0, common_1.Post)('report'),
    (0, common_1.HttpCode)(common_1.HttpStatus.OK),
    (0, swagger_1.ApiOperation)({ summary: 'AI 탐험 리포트 생성',
        description: '사용자의 방문 기록을 기반으로 AI탐험 리포트를 생성합니다'
    }),
    (0, swagger_1.ApiResponse)({ status: common_1.HttpStatus.OK, description: 'AI 탐험 리포트 생성 성공',
        type: aiReport_dto_1.AiReportApiDto }),
    (0, swagger_1.ApiResponse)({ status: common_1.HttpStatus.BAD_REQUEST,
        description: '잘못된 요청 데이터' }),
    (0, swagger_1.ApiResponse)({ status: common_1.HttpStatus.INTERNAL_SERVER_ERROR,
        description: '서버 오류 또는 AI 호출 실패' }),
    __param(0, (0, common_1.Body)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [aiReport_dto_1.CreateReportDto]),
    __metadata("design:returntype", Promise)
], AiController.prototype, "createReport", null);
exports.AiController = AiController = __decorate([
    (0, swagger_1.ApiTags)('AI 탐험 리포트'),
    (0, common_1.Controller)('api/ai'),
    __metadata("design:paramtypes", [ai_service_1.AiService])
], AiController);
//# sourceMappingURL=ai.controller.js.map