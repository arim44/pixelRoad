"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.LandmarkService = void 0;
const common_1 = require("@nestjs/common");
const promises_1 = require("fs/promises");
const path_1 = require("path");
let LandmarkService = class LandmarkService {
    filePath = (0, path_1.join)(process.cwd(), 'data', 'landmarks.json');
    async getAllLandmarks() {
        const data = await (0, promises_1.readFile)(this.filePath, 'utf-8');
        return JSON.parse(data);
    }
    async getLandmarkById(id) {
        const landmarks = await this.getAllLandmarks();
        return landmarks.find((landmark) => landmark.id === id);
    }
};
exports.LandmarkService = LandmarkService;
exports.LandmarkService = LandmarkService = __decorate([
    (0, common_1.Injectable)()
], LandmarkService);
//# sourceMappingURL=landmark.service.js.map